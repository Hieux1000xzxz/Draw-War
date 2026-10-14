using System.Collections.Generic;
using UnityEngine;

namespace ArtWar
{
    /// <summary>A drawing brought to life: walks or flies to the nearest enemy it can hit, attacks, and tears apart on death.</summary>
    public class DoodleUnit : MonoBehaviour
    {
        const float FlyHeight = 2.6f;
        const float GhostHeight = 0.55f; // ghosts drift low, just above the swords
        const float BatHeight = 1.5f;    // bats flit about head height
        const float BalloonHeight = 3.4f; // balloons float high and slow

        public Team Team { get; private set; }
        public UnitStats Stats { get; private set; }
        public bool IsBase { get; private set; }
        public bool IsFlying => Stats.flying;
        public float Radius { get; private set; }
        public float Height { get; private set; }
        public float Hp { get; private set; }
        public bool IsAlive => Hp > 0f;
        public UnitKind Kind => Stats.kind;

        /// <summary>Same number on both devices in an online game.</summary>
        public int NetId { get; set; }
        /// <summary>On the guest's device: a puppet that only shows what the host sends.</summary>
        public bool Remote { get; set; }
        /// <summary>How many attacks it has made (sent online so the guest can play them).</summary>
        public int AttackCount { get; private set; }
        Vector3 remoteTarget;
        bool remoteMoving;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color HitTint = new Color(1f, 0.55f, 0.5f);
        static readonly Color HealTint = new Color(0.6f, 1f, 0.7f);

        Transform visual;
        Transform shadow;
        List<GameObject> pieces;
        Renderer[] renderers;
        Transform hpFill;
        float hpBarWidth;
        float attackTimer;
        float walkPhase;
        float lungeTime = -1f;
        float hitTime = -1f;
        float spawnTime;
        float altitude;
        float heading;      // current facing; jets turn around at the field edges
        bool chargeReady = true;
        MaterialPropertyBlock block;
        DoodleRig rig;
        DoodleUnit lookTarget;
        float lastX;
        float kneel, kneelTarget;   // riflemen drop to one knee to shoot
        int burstLeft;
        int breaths;               // dragons roar every third breath
        float diveTime = -1f;      // dragons swoop down while breathing fire
        Transform jaws;
        float reloadTimer;

        float TeamFacing => Team == Team.Player ? 1f : -1f;
        public float Facing => heading;

        /// <summary>Point in the middle of the body, used to aim shots.</summary>
        public Vector3 Center => transform.position + Vector3.up * (Height * 0.5f);

        public bool CanHit(DoodleUnit other) => !other.IsFlying || Stats.HitsAir;

        float slowUntil;

        /// <summary>Walking speed, halved while slowed by venom or snow.</summary>
        float Speed => Stats.speed * (Time.time < slowUntil ? 0.5f : 1f);

        /// <summary>Slows this unit for a while (spider venom, snowballs).</summary>
        public void Slow(float seconds)
        {
            if (IsBase) return;
            slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
        }

        /// <summary>Little creatures (soldiers, riders, bats) squeak, yell and scream like cartoon cockroaches; machines don't.</summary>
        public bool HasVoice => !IsBase && (Kind == UnitKind.Infantry || Kind == UnitKind.Cavalry || Kind == UnitKind.Archer ||
            Kind == UnitKind.Healer || Kind == UnitKind.Rifleman || Kind == UnitKind.Shieldman || Kind == UnitKind.Grenadier ||
            Kind == UnitKind.Bat);

        /// <summary>Each little soldier has its own voice: smaller ones squeak higher.</summary>
        public float VoicePitch => Mathf.Lerp(1.05f, 0.85f, Stats.size) * voiceTone;
        float voiceTone = 1f;

        public void Init(Doodle doodle, Team team, UnitStats stats, bool isBase, float scale = 1f)
        {
            Team = team;
            Stats = stats;
            IsBase = isBase;
            Hp = stats.maxHp;
            heading = TeamFacing;
            voiceTone = Random.Range(0.9f, 1.1f);

            // A castle is grey stone with a banner in the team's colour, no face; units are the player's drawing.
            var castleOrigin = CastleOrigin;
            castleScale = scale;
            var built = isBase
                ? DoodleBuilder.BuildScenery(doodle, new Color(0.66f, 0.64f, 0.63f), scale, castleOrigin, 1f)
                : DoodleBuilder.Build(doodle, team == Team.Enemy, team, scale, true);
            if (isBase)
            {
                var banner = DoodleBuilder.BuildScenery(DoodleTemplates.CastleBanner(InkColor.Black),
                    team == Team.Player ? new Color(0.35f, 0.55f, 1f) : new Color(1f, 0.35f, 0.3f), scale, castleOrigin, 0.97f);
                foreach (var p in banner.parts)
                {
                    p.go.transform.SetParent(built.root.transform, true);
                    built.parts.Add(p);
                }
                Object.Destroy(banner.root);
            }
            rig = new DoodleRig(built, stats);
            if (stats.kind == UnitKind.Dragon) MakeFierce(built);
            visual = built.root.transform;
            visual.SetParent(transform, false);
            pieces = built.parts.ConvertAll(p => p.go);
            renderers = pieces.ConvertAll(p => p.GetComponent<Renderer>()).ToArray();
            Vector2 size = built.size;
            Radius = Mathf.Max(size.x * 0.5f, 0.3f);
            Height = size.y;
            lastX = transform.position.x;

            shadow = DoodleBuilder.BuildShadow(Radius * 1.1f, 0.3f + Radius * 0.15f).transform;
            shadow.SetParent(transform, false);

            BuildHpBar(size.y);

            block = new MaterialPropertyBlock();
            attackTimer = stats.attackInterval * 0.5f;
            walkPhase = Random.value * 10f;
            spawnTime = Time.time;
            reloadTimer = Random.Range(0f, 1.2f); // squad mates open fire at different moments

            if (!isBase) Sparkle();
        }

        /// <summary>Dragons get angry slanted brows over their eyes and remember where their head is (fire comes from the jaws).</summary>
        void MakeFierce(BuiltDoodle built)
        {
            float brow = Mathf.Max(built.eyeRadius, 0.05f);
            float facing = built.mirrored ? -1f : 1f;
            foreach (var eye in built.eyes)
            {
                var q = MakeQuad(eye, new Color(0.12f, 0.08f, 0.1f));
                q.name = "Angry Brow";
                q.localPosition = new Vector3(0f, brow * 1.45f, -0.02f);
                q.localRotation = Quaternion.Euler(0f, 0f, -24f * facing); // slanting down toward the snout
                q.localScale = new Vector3(brow * 2.6f, brow * 0.5f, 1f);
            }
            foreach (var p in built.parts)
                if (p.isHead) jaws = p.go.transform;
        }

        /// <summary>Front tip of the head, where dragon fire comes out.</summary>
        bool TryGetJaws(out Vector3 world)
        {
            world = Vector3.zero;
            if (jaws == null) return false;
            var mf = jaws.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            var b = mf.sharedMesh.bounds;
            // the snout is on the drawing's front side; a turned-around flier is mirrored by the transform itself
            float x = Team == Team.Player ? b.max.x : b.min.x;
            world = jaws.TransformPoint(new Vector3(x, b.center.y - b.extents.y * 0.2f, 0f));
            return true;
        }

        /// <summary>A little burst of ink stars as the drawing comes to life.</summary>
        void Sparkle()
        {
            float loud = Team == Team.Player ? 1f : 0.45f; // the enemy drawing its army is quieter
            Sfx.Play(Sound.Boing, 0.7f * loud, Mathf.Lerp(1.3f, 0.75f, Stats.size)); // big drawings boing lower
            Sfx.Play(Sound.Twinkle, 0.4f * loud);
            if (Kind == UnitKind.Dragon) Sfx.Play(Sound.Roar, 0.8f * loud);
            if (Kind == UnitKind.Robot) Sfx.Play(Sound.Beep, 0.6f * loud);
            if (HasVoice && Random.value < 0.6f) Sfx.Play(Sound.WarCry, 0.7f * loud, VoicePitch); // "yaa!"
            Vector3 c = transform.position + Vector3.up * (Height * 0.5f);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + Random.value * 0.5f;
                Vector3 end = c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (Radius + Random.Range(0.4f, 0.9f));
                Projectile.Launch(DoodleTemplates.Sparkle(), Random.Range(0.12f, 0.2f), c, end, 3.5f, 0.3f, false, null);
            }
        }

        void BuildHpBar(float height)
        {
            hpBarWidth = Mathf.Clamp(Radius * 1.6f, 0.7f, 2.5f);
            var root = new GameObject("HP Bar").transform;
            root.SetParent(transform, false);
            // a castle's bar floats well clear of the banner on its keep
            root.localPosition = new Vector3(0f, height + (IsBase ? 0.9f : 0.25f), 0f);

            var bg = MakeQuad(root, new Color(0.2f, 0.17f, 0.15f));
            bg.localScale = new Vector3(hpBarWidth + 0.06f, 0.16f, 1f);
            bg.localPosition = new Vector3(0f, 0f, 0.01f);

            hpFill = MakeQuad(root, Team == Team.Player ? new Color(0.3f, 0.8f, 0.35f) : new Color(0.9f, 0.3f, 0.3f));
            UpdateHpBar();
        }

        static Transform MakeQuad(Transform parent, Color color)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = DoodleBuilder.ColorMaterial(color);
            q.transform.SetParent(parent, false);
            return q.transform;
        }

        void UpdateHpBar()
        {
            float ratio = Mathf.Clamp01(Hp / Stats.maxHp);
            hpFill.localScale = new Vector3(hpBarWidth * ratio, 0.1f, 1f);
            hpFill.localPosition = new Vector3(-(1f - ratio) * hpBarWidth * 0.5f, 0f, 0f);
        }

        void Update()
        {
            if (!IsAlive) return;
            if (Remote)
            {
                RemoteTick();
                return;
            }

            var mgr = BattleManager.Instance;
            bool moving = false;
            if (!IsBase && mgr != null && mgr.IsWar)
            {
                switch (Kind)
                {
                    case UnitKind.Jet: moving = ThinkJet(mgr); break;
                    case UnitKind.Healer: moving = ThinkHealer(mgr); break;
                    case UnitKind.Rifleman: moving = ThinkRifleman(mgr); break;
                    default: moving = ThinkFighter(mgr); break;
                }
            }
            else if (IsBase && mgr != null && mgr.IsWar) DefendCastle(mgr);

            if (IsFlying) UpdateFlight();
            Animate(moving);
        }

        // ---------------------------------------------------------------- behaviours

        bool ThinkFighter(BattleManager mgr)
        {
            var target = lookTarget = mgr.FindTarget(this);
            if (target == null) return false;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            // Fliers can pass over their prey, so they turn to face it (ground units always face the enemy side).
            if (IsFlying && Mathf.Abs(to.x) > 0.4f) heading = Mathf.Sign(to.x);
            if (to.magnitude > Reach(target))
            {
                if (Stats.Stationary) return false;
                MoveTowards(mgr, to);
                return true;
            }
            TickAttack(target);
            return false;
        }

        /// <summary>Jets never stop: they strafe across the field, fire at anything in front, and loop back at the edges.</summary>
        bool ThinkJet(BattleManager mgr)
        {
            var p = transform.position;
            if (p.x * heading > mgr.fieldHalfX + 1.5f) heading = -heading;

            var target = lookTarget = mgr.FindTarget(this);
            Vector3 move = new Vector3(heading * Speed, 0f, 0f);
            if (target != null)
            {
                Vector3 to = target.transform.position - p;
                move.z = Mathf.Clamp(to.z, -1f, 1f) * 1.5f;
                bool ahead = to.x * heading > 0.5f;
                to.y = 0f;
                if (ahead && to.magnitude <= Stats.range + target.Radius) TickAttack(target);
            }
            transform.position = mgr.ClampToField(p + move * Time.deltaTime);
            return true;
        }

        /// <summary>
        /// Riflemen advance at a steady pace, then drop to one knee, take aim and fire short bursts with a
        /// reload pause in between. Squad mates start their reload at different times, so the squad fires in turns.
        /// </summary>
        bool ThinkRifleman(BattleManager mgr)
        {
            var target = lookTarget = mgr.FindTarget(this);
            if (target == null)
            {
                kneelTarget = 0f;
                return false;
            }

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > Stats.range + target.Radius)
            {
                kneelTarget = 0f;
                burstLeft = 0;
                if (kneel > 0.05f) return false; // get up before moving off
                MoveTowards(mgr, to);
                return true;
            }

            kneelTarget = 1f;
            if (kneel < 0.9f) return false; // settle into position before the first shot

            if (burstLeft > 0)
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    attackTimer = Stats.attackInterval;
                    burstLeft--;
                    FireRifle(target);
                }
            }
            else
            {
                reloadTimer -= Time.deltaTime;
                if (reloadTimer <= 0f)
                {
                    burstLeft = 3;
                    attackTimer = 0f;
                    reloadTimer = 1f + Random.Range(0f, 0.4f);
                }
            }
            return false;
        }

        void FireRifle(DoodleUnit target)
        {
            rig.Attack();
            Sfx.Play(Sound.Pew, 0.4f);
            if (!rig.TryGetMuzzle(out Vector3 muzzle))
                muzzle = transform.position + new Vector3(Facing * Radius, Height * 0.6f, 0f);
            float dmg = Stats.damage;
            Vector3 aimPoint = target.Center + Random.insideUnitSphere * 0.15f;
            Projectile.Launch(DoodleTemplates.BulletShot(), 0.55f, muzzle, aimPoint, 24f, 0f, true,
                () => { if (target != null && target.IsAlive) target.TakeDamage(dmg * Counter(target)); });
            Projectile.Launch(DoodleTemplates.MuzzleFlash(), 0.16f, muzzle, muzzle + new Vector3(Facing * 0.1f, 0f, 0f),
                0.8f, 0f, false, null); // lingers ~0.12 s
        }

        /// <summary>Healers go to the most hurt ally nearby; otherwise they tag along behind the nearest friend.</summary>
        bool ThinkHealer(BattleManager mgr)
        {
            var patient = lookTarget = mgr.FindHurtAlly(this);
            if (patient != null)
            {
                Vector3 to = patient.transform.position - transform.position;
                to.y = 0f;
                if (to.magnitude > Stats.range + patient.Radius)
                {
                    MoveTowards(mgr, to);
                    return true;
                }
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    attackTimer = Stats.attackInterval;
                    lungeTime = 0f;
                    rig.Attack();
                    Sfx.Play(Sound.Heal, 0.5f);
                    float amount = Stats.damage;
                    Projectile.Launch(DoodleTemplates.HealPuff(), 0.3f, Center, patient.Center, 6f, 0.6f, false,
                        () => { if (patient != null && patient.IsAlive) patient.Heal(amount); });
                }
                return false;
            }

            var friend = lookTarget = mgr.FindNearestAlly(this);
            if (friend == null) return false;
            Vector3 toFriend = friend.transform.position - transform.position;
            toFriend.y = 0f;
            if (toFriend.magnitude <= Radius + friend.Radius + 1f) return false;
            MoveTowards(mgr, toFriend);
            return true;
        }

        void MoveTowards(BattleManager mgr, Vector3 to)
        {
            if (Mathf.Abs(to.x) > 0.05f && !IsFlying) heading = TeamFacing; // ground units keep facing the enemy
            Vector3 move = to.normalized * Speed + mgr.Separation(this);
            transform.position = mgr.ClampToField(transform.position + move * Time.deltaTime);
        }

        void TickAttack(DoodleUnit target)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;
            attackTimer = Stats.attackInterval;
            Attack(target);
        }

        float Reach(DoodleUnit target)
        {
            switch (Kind)
            {
                case UnitKind.Plane:
                case UnitKind.Balloon: return 0.3f + target.Radius * 0.5f; // fly over it and drop bombs
                case UnitKind.Infantry:
                case UnitKind.Cavalry:
                case UnitKind.Wall:
                case UnitKind.Ghost:
                case UnitKind.Bat:
                case UnitKind.Spider: return Radius + target.Radius + 0.1f;
                case UnitKind.Snake: return Radius + target.Radius + 0.1f + Stats.range; // strikes from a coil
                case UnitKind.Shieldman: return Radius + target.Radius + 0.1f + Stats.range; // the spear reaches over the shield
                case UnitKind.Robot:
                    // fists and swords fight up close; cannon and jet robots shoot from range
                    return Stats.range > 0f ? Stats.range + target.Radius : Radius + target.Radius + 0.1f;
                default: return Stats.range + target.Radius;
            }
        }

        void Attack(DoodleUnit target)
        {
            var mgr = BattleManager.Instance;
            AttackCount++;
            lungeTime = 0f;
            rig.Attack();
            float dmg = Stats.damage;
            Vector3 muzzle = transform.position + new Vector3(Facing * Radius * 0.8f, Height * 0.55f, 0f);
            PlayAttackSound();

            switch (Kind)
            {
                case UnitKind.Wall:
                    break;

                case UnitKind.Archer:
                case UnitKind.Turret:
                    Projectile.Launch(DoodleTemplates.ArrowShot(), 0.35f, muzzle, target.Center, 14f, 0.8f, true,
                        () => { if (target != null && target.IsAlive) target.TakeDamage(dmg * Counter(target)); });
                    break;

                case UnitKind.Helicopter:
                    Projectile.Launch(DoodleTemplates.BulletShot(), 0.35f, muzzle, target.Center, 18f, 0f, true,
                        () => { if (target != null && target.IsAlive) target.TakeDamage(dmg * Counter(target)); });
                    break;

                case UnitKind.Jet:
                    Projectile.Launch(DoodleTemplates.MissileShot(), 0.35f, muzzle, target.Center, 16f, 0.2f, true,
                        () => { if (target != null && target.IsAlive) target.TakeDamage(dmg * Counter(target)); });
                    break;

                case UnitKind.Tank:
                {
                    Vector3 impact = target.transform.position;
                    Projectile.Launch(DoodleTemplates.BallShot(InkColor.Black), 0.3f, muzzle, impact, 9f, 2.5f, false,
                        () => { Sfx.Play(Sound.Boom, 0.8f); mgr.DamageArea(this, impact, Stats.splash, dmg, false); });
                    break;
                }

                case UnitKind.Plane:
                case UnitKind.Balloon:
                {
                    Vector3 impact = target.transform.position;
                    Vector3 belly = transform.position + Vector3.up * (Height * 0.3f);
                    Projectile.Launch(DoodleTemplates.BallShot(InkColor.Black), 0.25f, belly, impact, 7f, 0f, false,
                        () => { Sfx.Play(Sound.Boom, 0.8f); mgr.DamageArea(this, impact, Stats.splash, dmg, false); });
                    break;
                }

                case UnitKind.Dragon:
                {
                    // swoop down, roar now and then, and pour a wide cone of fire from the jaws
                    diveTime = 0f;
                    if (++breaths % 3 == 1) Sfx.Play(Sound.Roar, 0.8f, Mathf.Lerp(1.15f, 0.8f, Stats.size));
                    mgr.Breath(this, Stats.range, dmg);
                    if (!TryGetJaws(out Vector3 jaws)) jaws = muzzle;
                    // a cone of flame tongues fanning out over the target, orange and yellow
                    Vector3 aim = target.Center;
                    for (int i = 0; i < 10; i++)
                    {
                        Vector3 end = aim + new Vector3(Random.Range(-0.9f, 0.9f), Random.Range(-0.4f, 0.4f), Random.Range(-0.9f, 0.9f));
                        Color flame = i % 3 == 0 ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.5f, 0.12f);
                        Projectile.Launch(DoodleTemplates.FirePuff(), Random.Range(0.5f, 0.8f), jaws, end, Random.Range(7f, 10f),
                            0.15f, false, null, flame);
                    }
                    break;
                }

                case UnitKind.Robot:
                {
                    Vector3 impact = target.transform.position;
                    if (!rig.TryGetMuzzle(out Vector3 barrel)) barrel = muzzle;
                    switch (Stats.robotMode)
                    {
                        case RobotMode.Cannon: // arm cannon: a high shell that bursts where it lands
                            Projectile.Launch(DoodleTemplates.BallShot(InkColor.Black), 0.4f, barrel, impact, 9f, 3f, false,
                                () => { Sfx.Play(Sound.Boom, 0.9f); mgr.DamageArea(this, impact, Stats.splash, dmg, false); });
                            break;
                        case RobotMode.Flyer: // rockets from the wings, at ground or air
                            Projectile.Launch(DoodleTemplates.MissileShot(), 0.4f, muzzle, target.Center, 15f, 0.3f, true,
                                () => { if (target != null && target.IsAlive) target.TakeDamage(dmg * Counter(target)); });
                            break;
                        default: // fists pound the ground; a sword's sweep also clips whoever stands beside the target
                            mgr.DamageArea(this, impact, Stats.splash, dmg, false);
                            break;
                    }
                    break;
                }

                case UnitKind.HorseArcher:
                    Projectile.Launch(DoodleTemplates.ArrowShot(), 0.35f, muzzle, target.Center, 15f, 0.7f, true,
                        () => { if (target != null && target.IsAlive) target.TakeDamage(dmg * Counter(target)); });
                    break;

                case UnitKind.Wizard:
                {
                    // a purple magic bolt that bursts where it lands (and finds flying targets too)
                    Vector3 aim = target.Center;
                    bool air = target.IsFlying;
                    Projectile.Launch(DoodleTemplates.Sparkle(), 0.5f, muzzle, aim, 10f, 0.4f, false,
                        () =>
                        {
                            Sfx.Play(Sound.Twinkle, 0.5f, 0.7f);
                            mgr.DamageArea(this, aim, Stats.splash, dmg, air);
                        }, new Color(0.7f, 0.45f, 1f));
                    break;
                }

                case UnitKind.Snake:
                case UnitKind.Spider:
                    // a venomous bite: hurts a little and slows the victim down
                    target.TakeDamage(dmg * Counter(target));
                    target.Slow(1.5f);
                    break;

                case UnitKind.Snowman:
                    // a big snowball on a high lob; whoever it hits is slowed by the cold
                    Projectile.Launch(DoodleTemplates.BallShot(InkColor.Black), 0.35f, muzzle, target.Center, 8f, 1.6f, false,
                        () =>
                        {
                            if (target == null || !target.IsAlive) return;
                            target.TakeDamage(dmg * Counter(target));
                            target.Slow(2.5f);
                        }, new Color(0.95f, 0.98f, 1f));
                    break;

                case UnitKind.Grenadier:
                {
                    // a lit bomb lobbed high over the front line, bursting where it lands
                    Vector3 impact = target.transform.position;
                    Projectile.Launch(DoodleTemplates.BallShot(InkColor.Black), 0.3f, muzzle, impact, 7f, 2.5f, false,
                        () => { Sfx.Play(Sound.Boom, 0.7f, 1.2f); mgr.DamageArea(this, impact, Stats.splash, dmg, false); });
                    break;
                }

                case UnitKind.Catapult:
                {
                    // the arm swings and flings a big boulder on a tall arc
                    Vector3 impact = target.transform.position;
                    Vector3 bucket = transform.position + new Vector3(Facing * Radius * 0.6f, Height * 0.9f, 0f);
                    Projectile.Launch(DoodleTemplates.BallShot(InkColor.Black), 0.6f, bucket, impact, 8f, 4.5f, false,
                        () => { Sfx.Play(Sound.Boom, 1f, 0.75f); mgr.DamageArea(this, impact, Stats.splash, dmg, false); });
                    break;
                }

                case UnitKind.Ghost:
                    // a chilling touch that drains life into the ghost
                    target.TakeDamage(dmg * Counter(target));
                    if (Hp < Stats.maxHp) Heal(dmg * 0.5f);
                    break;

                case UnitKind.Cavalry:
                    // The first hit of a charge lands twice as hard.
                    target.TakeDamage(dmg * Counter(target) * (chargeReady ? 2f : 1f));
                    chargeReady = false;
                    break;

                default:
                    target.TakeDamage(dmg * Counter(target));
                    break;
            }
        }

        const float CastleRange = 7f;
        const float CastleDamage = 14f;
        const float CastleReload = 1f;
        const float CannonDamage = 40f;
        const float CannonSplash = 1.5f;

        public const int CastleMaxLevel = 5;
        public int CastleLevel { get; private set; } = 1;
        float cannonTimer = 1.5f;
        float castleScale = 1f;
        static readonly Vector2 CastleOrigin = new Vector2(0.5f, 0.05f);

        /// <summary>Ink to go up to <paramref name="level"/>: 150, 250, 350, 450.</summary>
        public static float UpgradeCost(int level) => 50f + 100f * (level - 1);

        /// <summary>What the next castle level brings, for the upgrade button.</summary>
        public static string UpgradeInfo(int level)
        {
            switch (level)
            {
                case 2: return Loc.T("+400 máu · tường giáp 15% · cọc gỗ · bắn 2 tên", "+400 HP · walls block 15% · wooden stakes · 2 arrows a volley");
                case 3: return Loc.T("+400 máu · đại bác trên tháp", "+400 HP · cannon on the tower");
                case 4: return Loc.T("+400 máu · tên mạnh hơn, xa hơn · đại bác thứ 2", "+400 HP · stronger, longer arrows · a 2nd cannon");
                case 5: return Loc.T("+500 máu · tường giáp 30% · đại bác nạp nhanh", "+500 HP · walls block 30% · faster cannons");
                default: return "";
            }
        }

        /// <summary>Spends a level on the castle: thicker walls and more firepower, each one drawn onto it.</summary>
        public void UpgradeCastle()
        {
            if (!IsBase || CastleLevel >= CastleMaxLevel) return;
            CastleLevel++;
            var s = Stats;
            float extra = CastleLevel == 5 ? 500f : 400f;
            s.maxHp += extra;
            s.armor = CastleLevel >= 5 ? 0.3f : 0.15f;
            Stats = s;
            Hp = Mathf.Min(s.maxHp, Hp + extra); // the new stonework also patches the walls
            UpdateHpBar();

            bool right = TeamFacing > 0f;
            switch (CastleLevel)
            {
                case 2: AddCastleDecor(DoodleTemplates.CastleStakes(), new Color(0.62f, 0.44f, 0.26f)); break;
                case 3: AddCastleDecor(DoodleTemplates.CastleCannon(right, false), new Color(0.32f, 0.32f, 0.36f)); break;
                case 4: AddCastleDecor(DoodleTemplates.CastleCannon(right, true), new Color(0.32f, 0.32f, 0.36f)); break;
                case 5: AddCastleDecor(DoodleTemplates.CastlePennants(), new Color(1f, 0.8f, 0.2f)); break;
            }
            Sfx.Play(Sound.Clank, 0.7f, 0.8f);
            Sfx.Play(Sound.Twinkle, 0.6f);
            hitTime = 0f;
            SetTint(HealTint);
        }

        /// <summary>Draws an upgrade onto the castle, in the castle's own canvas space so it lines up.</summary>
        void AddCastleDecor(Doodle doodle, Color fill)
        {
            var built = DoodleBuilder.BuildScenery(doodle, fill, castleScale, CastleOrigin, 1f);
            foreach (var p in built.parts)
            {
                p.go.transform.SetParent(visual, false);
                pieces.Add(p.go);
            }
            Object.Destroy(built.root);
            renderers = pieces.ConvertAll(p => p.GetComponent<Renderer>()).ToArray();
        }

        /// <summary>
        /// Castles defend themselves: archers on the walls shoot the nearest enemies in range, flying or not
        /// (two at a time from level 2, harder and farther from level 4); from level 3 cannons on the towers
        /// lob shells that burst among ground troops.
        /// </summary>
        void DefendCastle(BattleManager mgr)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                float range = CastleRange + (CastleLevel >= 4 ? 1.5f : 0f);
                var targets = mgr.FindEnemies(this, range + Radius);
                if (targets.Count > 0)
                {
                    attackTimer = CastleReload;
                    float damage = CastleDamage * (CastleLevel >= 4 ? 1.5f : 1f);
                    int arrows = CastleLevel >= 2 ? 2 : 1;
                    Sfx.Play(Sound.Fwip, 0.5f);
                    for (int i = 0; i < arrows; i++)
                    {
                        var target = targets[i % targets.Count];
                        Vector3 battlements = transform.position +
                            new Vector3(Facing * Radius * (0.4f - 0.5f * i), Height * 0.72f, 0f);
                        Projectile.Launch(DoodleTemplates.ArrowShot(), 0.35f, battlements, target.Center, 14f, 1f, true,
                            () => { if (target != null && target.IsAlive) target.TakeDamage(damage); });
                    }
                }
            }

            if (CastleLevel < 3) return;
            cannonTimer -= Time.deltaTime;
            if (cannonTimer > 0f) return;
            var ground = mgr.FindEnemies(this, CastleRange + 2f + Radius, true);
            if (ground.Count == 0) return;
            cannonTimer = CastleLevel >= 5 ? 2f : 3f;
            int cannons = CastleLevel >= 4 ? 2 : 1;
            for (int i = 0; i < cannons; i++)
            {
                // the tower cannon, then the one on the keep
                Vector3 muzzle = transform.position + (i == 0
                    ? new Vector3(Facing * Radius * 0.95f, Height * 0.68f, 0f)
                    : new Vector3(Facing * Radius * 0.4f, Height * 0.84f, 0f));
                Vector3 impact = ground[i % ground.Count].transform.position;
                Projectile.Launch(DoodleTemplates.BallShot(InkColor.Black), 0.4f, muzzle, impact, 9f, 3f, false,
                    () => { Sfx.Play(Sound.Boom, 0.9f); mgr.DamageArea(this, impact, CannonSplash, CannonDamage, false); });
            }
            Sfx.Play(Sound.Cannon, 0.9f);
        }

        void PlayAttackSound()
        {
            switch (Kind)
            {
                case UnitKind.Infantry:
                case UnitKind.Cavalry: Sfx.Play(Sound.Swish, 0.6f); break;
                case UnitKind.Robot:
                    switch (Stats.robotMode)
                    {
                        case RobotMode.Cannon: Sfx.Play(Sound.Cannon, 0.9f); break;
                        case RobotMode.Flyer: Sfx.Play(Sound.Missile, 0.6f); break;
                        case RobotMode.Sword: Sfx.Play(Sound.Swish, 0.7f, 0.8f); Sfx.Play(Sound.Clank, 0.5f, 1.3f); break;
                        default: Sfx.Play(Sound.Clank, 0.7f); break;
                    }
                    break;
                case UnitKind.Archer:
                case UnitKind.Turret: Sfx.Play(Sound.Fwip, 0.6f); break;
                case UnitKind.Helicopter: Sfx.Play(Sound.Pew, 0.35f, 1.4f); break;
                case UnitKind.Jet: Sfx.Play(Sound.Missile, 0.6f); break;
                case UnitKind.Tank: Sfx.Play(Sound.Cannon, 0.8f); break;
                case UnitKind.Plane: Sfx.Play(Sound.BombWhistle, 0.5f); break;
                case UnitKind.Dragon: Sfx.Play(Sound.Fire, 0.7f); break;
                case UnitKind.Shieldman: Sfx.Play(Sound.Swish, 0.5f, 0.9f); Sfx.Play(Sound.Bonk, 0.4f, 0.8f); break; // spear thrust, shield shove
                case UnitKind.Ghost: if (Random.value < 0.5f) Sfx.Play(Sound.Boo, 0.55f, Random.Range(0.9f, 1.15f)); break;
                case UnitKind.Grenadier: Sfx.Play(Sound.Fwip, 0.5f, 0.7f); if (Random.value < 0.3f) Sfx.Play(Sound.Giggle, 0.7f, VoicePitch); break;
                case UnitKind.Catapult: Sfx.Play(Sound.Swish, 0.8f, 0.55f); Sfx.Play(Sound.Clank, 0.4f, 0.6f); break;
                case UnitKind.Bat: Sfx.Play(Sound.Squeak, 0.35f, 1.8f); break;
                case UnitKind.Spider: Sfx.Play(Sound.Squeak, 0.3f, 2.2f); break;
                case UnitKind.Snake: Sfx.Play(Sound.Swish, 0.4f, 1.8f); break;
                case UnitKind.Wizard: Sfx.Play(Sound.Twinkle, 0.45f, 1.3f); break;
                case UnitKind.HorseArcher: Sfx.Play(Sound.Fwip, 0.6f); break;
                case UnitKind.Snowman: Sfx.Play(Sound.Fwip, 0.5f, 0.6f); break;
                case UnitKind.Balloon: Sfx.Play(Sound.BombWhistle, 0.45f, 0.9f); break;
            }
        }

        /// <summary>Rock-paper-scissors bonuses between unit kinds.</summary>
        public float Counter(DoodleUnit target)
        {
            if (target.IsBase) return Kind == UnitKind.Catapult ? 1.8f : 1f; // boulders crack castle walls
            var k = target.Kind;
            switch (Kind)
            {
                case UnitKind.Archer:
                case UnitKind.Turret: return target.IsFlying ? 2f : 1f;
                case UnitKind.Jet: return target.IsFlying ? 2f : 1f;
                case UnitKind.Plane:
                    return k == UnitKind.Tank ? 2f : k == UnitKind.Wall || k == UnitKind.Turret || k == UnitKind.Robot ? 1.5f : 1f;
                case UnitKind.Helicopter: return k == UnitKind.Tank ? 1.5f : 1f;
                case UnitKind.Tank:
                    return k == UnitKind.Infantry || k == UnitKind.Archer || k == UnitKind.Cavalry || k == UnitKind.Wall ||
                           k == UnitKind.Rifleman ? 1.5f : 1f;
                case UnitKind.Infantry: return k == UnitKind.Archer || k == UnitKind.Healer ? 1.5f : 1f;
                case UnitKind.Cavalry:
                    return k == UnitKind.Archer || k == UnitKind.Turret || k == UnitKind.Healer || k == UnitKind.Rifleman
                        ? 1.5f : 1f;
                case UnitKind.Rifleman: return k == UnitKind.Infantry || k == UnitKind.Healer ? 1.3f : 1f;
                case UnitKind.Shieldman: return k == UnitKind.Cavalry ? 1.8f : k == UnitKind.Infantry ? 1.2f : 1f; // spears stop horses
                case UnitKind.Grenadier: // bombs drop over shields onto packed soldiers
                    return k == UnitKind.Shieldman || k == UnitKind.Infantry || k == UnitKind.Rifleman ? 1.4f : 1f;
                case UnitKind.Catapult: return k == UnitKind.Wall || k == UnitKind.Turret || k == UnitKind.Tank ? 1.5f : 1f;
                case UnitKind.Spider: return k == UnitKind.Archer || k == UnitKind.Healer || k == UnitKind.Grenadier ? 1.4f : 1f;
                case UnitKind.Snake: return k == UnitKind.Healer || k == UnitKind.Wizard || k == UnitKind.Rifleman ? 1.4f : 1f;
                case UnitKind.Wizard: return target.IsFlying || k == UnitKind.Robot || k == UnitKind.Shieldman ? 1.4f : 1f; // magic goes through armour
                case UnitKind.HorseArcher: return target.IsFlying ? 1.8f : 1f;
                case UnitKind.Snowman: return k == UnitKind.Cavalry || k == UnitKind.Spider ? 1.4f : 1f; // snow stops fast runners
                case UnitKind.Balloon:
                    return k == UnitKind.Tank || k == UnitKind.Wall || k == UnitKind.Turret || k == UnitKind.Catapult ? 1.5f : 1f;
                case UnitKind.Bat: // harasses the back line
                    return k == UnitKind.Healer || k == UnitKind.Archer || k == UnitKind.Catapult || k == UnitKind.Grenadier ? 1.5f : 1f;
                case UnitKind.Ghost:
                    return k == UnitKind.Healer || k == UnitKind.Archer || k == UnitKind.Rifleman || k == UnitKind.Turret ? 1.3f : 1f;
                case UnitKind.Robot:
                    return k == UnitKind.Tank || k == UnitKind.Wall ? 1.5f
                        : k == UnitKind.Infantry || k == UnitKind.Rifleman || k == UnitKind.Cavalry ? 1.3f : 1f;
                case UnitKind.Dragon:
                    return k == UnitKind.Infantry || k == UnitKind.Cavalry || k == UnitKind.Archer || k == UnitKind.Rifleman
                        ? 1.3f : 1f;
                default: return 1f;
            }
        }

        // ---------------------------------------------------------------- visuals

        void UpdateFlight()
        {
            // Take off after peeling off the page, then hover.
            float cruise = Kind == UnitKind.Ghost ? GhostHeight : Kind == UnitKind.Bat ? BatHeight
                : Kind == UnitKind.Balloon ? BalloonHeight : FlyHeight;
            float targetAltitude = Time.time - spawnTime > 0.45f ? cruise : 0f;
            float climb = 3f;
            if (diveTime >= 0f)
            {
                // a dragon's fire run: dip low and fast, then climb back
                diveTime += Time.deltaTime;
                if (diveTime < 0.35f) { targetAltitude = FlyHeight - 1.2f; climb = 7f; }
                else if (diveTime > 0.9f) diveTime = -1f;
            }
            altitude = Mathf.MoveTowards(altitude, targetAltitude, Time.deltaTime * climb);
            var p = transform.position;
            p.y = altitude + Mathf.Sin(Time.time * 2f + walkPhase) * 0.12f;
            transform.position = p;
            shadow.localPosition = new Vector3(0f, -p.y, 0f);
        }

        void Animate(bool moving)
        {
            float dt = Time.deltaTime;

            // Peel off the page: start lying flat, flip upright.
            float stand = Mathf.Clamp01((Time.time - spawnTime) / 0.45f);
            float ease = 1f - Mathf.Pow(1f - stand, 3f);
            float standAngle = Mathf.Lerp(90f, 0f, ease);

            bool hasLegs = Stats.legs > 0;
            float bob = 0f, tilt = 0f;
            if (moving)
            {
                walkPhase += dt * (4f + Stats.speed * 2.5f);
                if (IsFlying) tilt = -6f * Facing; // lean into the flight
                else if (Kind == UnitKind.Tank) bob = Mathf.Abs(Mathf.Sin(Time.time * 25f)) * 0.02f; // engine rumble
                else if (hasLegs)
                {
                    // two steps per cycle: the body rises on each step and leans forward
                    bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.09f;
                    tilt = -5f * Facing + Mathf.Sin(walkPhase) * 2.5f;
                }
                else
                {
                    // no legs: hop along
                    bob = Mathf.Abs(Mathf.Sin(walkPhase * 0.7f)) * 0.25f;
                    tilt = Mathf.Sin(walkPhase * 0.7f) * 5f;
                }
            }
            else if (!IsBase && !Stats.Stationary)
            {
                // idle breathing while waiting for the battle
                bob = (Mathf.Sin(Time.time * 2.5f + walkPhase) * 0.5f + 0.5f) * 0.03f;
            }
            if (Kind == UnitKind.Helicopter) tilt += Mathf.Sin(Time.time * 9f) * 1.5f; // rotor wobble
            if (Kind == UnitKind.Ghost) tilt += Mathf.Sin(Time.time * 3f + walkPhase) * 7f; // the sheet sways as it drifts

            float lunge = 0f;
            if (lungeTime >= 0f)
            {
                lungeTime += dt;
                float k = lungeTime / 0.25f;
                if (k >= 1f) lungeTime = -1f;
                else
                {
                    // melee lunges forward, shooters recoil
                    bool melee = Kind == UnitKind.Infantry || Kind == UnitKind.Cavalry || Kind == UnitKind.Dragon || Kind == UnitKind.Robot ||
                                 Kind == UnitKind.Shieldman || Kind == UnitKind.Ghost || Kind == UnitKind.Bat || Kind == UnitKind.Spider ||
                                 Kind == UnitKind.Snake;
                    lunge = Mathf.Sin(k * Mathf.PI) * 0.35f * Facing * (melee ? 1f : -0.4f);
                }
            }

            float squash = 0f;
            if (hitTime >= 0f)
            {
                hitTime += dt;
                float k = hitTime / 0.2f;
                if (k >= 1f)
                {
                    hitTime = -1f;
                    SetTint(null);
                }
                else squash = Mathf.Sin(k * Mathf.PI) * 0.15f;
            }

            // Riflemen: drop to one knee (the rig folds the rear leg) and sink a little.
            kneel = Mathf.MoveTowards(kneel, kneelTarget, dt * 4f);
            bob -= kneel * Height * 0.07f; // legs are ~35% of the height; a 35° fold drops the hips about this much

            float flip = Facing * TeamFacing; // -1 when a jet has turned around
            visual.localPosition = new Vector3(lunge, bob, 0f);
            visual.localRotation = Quaternion.Euler(standAngle, 0f, tilt);
            visual.localScale = new Vector3((1f + squash) * flip, 1f - squash, 1f);

            // Eyes follow the current target; otherwise look ahead.
            Vector3 dir = lookTarget != null && lookTarget.IsAlive
                ? lookTarget.Center - Center
                : new Vector3(Facing, Mathf.Sin(Time.time * 0.7f + walkPhase) * 0.3f, 0f);
            Vector2 look = new Vector2(dir.x * flip, dir.y);
            float travel = transform.position.x - lastX;
            lastX = transform.position.x;
            if (rig != null)
            {
                rig.Kneel = kneel;
                // riflemen aim once down on one knee; a robot's arm cannon tracks its target all the time
                bool aiming = kneel > 0.5f || (Kind == UnitKind.Robot && Stats.robotMode == RobotMode.Cannon && lookTarget != null);
                rig.Aim = aiming ? look : new Vector2(1f, 0.12f);
            }
            rig?.Tick(dt, moving, IsFlying && altitude > 0.5f, walkPhase, travel, look);
        }

        void SetTint(Color? tint)
        {
            if (block == null || renderers == null) return;
            block.Clear();
            if (tint.HasValue)
            {
                block.SetColor(BaseColorId, tint.Value);
                block.SetColor(ColorId, tint.Value);
            }
            foreach (var r in renderers)
                if (r != null) r.SetPropertyBlock(block, 1); // submesh 1 = main pen line
        }

        // ---------------------------------------------------------------- health

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            Hp = Mathf.Min(Stats.maxHp, Hp + amount);
            hitTime = 0f;
            SetTint(HealTint);
            UpdateHpBar();
        }

        public void TakeDamage(float damage)
        {
            if (!IsAlive) return;
            if (Stats.armor > 0f)
            {
                // the big shield takes part of every blow
                damage *= 1f - Stats.armor;
                if (Random.value < 0.4f) Sfx.Play(Sound.Clank, 0.3f, 1.4f);
            }
            Hp -= damage;
            hitTime = 0f;
            SetTint(HitTint);
            rig?.Hit();
            Sfx.Play(Sound.Bonk, 0.45f, IsBase ? 0.6f : 1f);
            if (HasVoice && Random.value < 0.35f) Sfx.Play(Sound.Yelp, 0.75f, VoicePitch); // "ái!"
            else if (!IsBase && !HasVoice && Random.value < 0.3f) Sfx.Play(Sound.Squeak, 0.3f, Mathf.Lerp(1.3f, 0.8f, Stats.size));
            UpdateHpBar();
            if (Hp <= 0f) Die();
        }

        void Die()
        {
            Hp = 0f;
            SetTint(null);
            Sfx.Play(Sound.Poof, IsBase ? 1f : 0.6f, IsBase ? 0.6f : 1f);
            if (HasVoice) Sfx.Play(Sound.Scream, 1f, VoicePitch); // the "AAAÉÉÉ!" as it tears apart
            else if (Kind == UnitKind.Ghost) Sfx.Play(Sound.Boo, 0.6f, 1.3f);
            Sfx.Play(Sound.Rip, 0.5f);
            visual.localScale = Vector3.one; // colliders don't like negative scale

            foreach (var piece in pieces)
            {
                if (piece == null) continue;
                piece.transform.SetParent(null, true);
                piece.AddComponent<BoxCollider>();
                var rb = piece.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.AddForce(new Vector3(-Facing * Random.Range(1f, 3f), Random.Range(2f, 5f), Random.Range(-1.5f, 1.5f)),
                    ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 0.3f, ForceMode.Impulse);
                piece.AddComponent<PaperScrap>();
            }

            if (BattleManager.Instance != null) BattleManager.Instance.OnUnitDied(this);
            Destroy(gameObject);
        }

        void OnDestroy() => DoodleBuilder.DestroyMeshes(gameObject);

        // ---------------------------------------------------------------- online puppet

        /// <summary>The host's latest word on this unit: where it is, its health, facing, castle level and attacks made.</summary>
        public void ApplyRemoteState(Vector3 position, float hp, float facing, int attacks, int castleLevel)
        {
            if (!IsAlive) return;
            remoteMoving = (position - remoteTarget).sqrMagnitude > 0.0004f;
            remoteTarget = position;
            if (facing != 0f) heading = facing;

            while (IsBase && CastleLevel < castleLevel) UpgradeCastle();
            if (hp < Hp - 0.01f)
            {
                // a hit: flash, wobble and squeak like the real thing
                hitTime = 0f;
                SetTint(HitTint);
                rig?.Hit();
                Sfx.Play(Sound.Bonk, 0.35f, IsBase ? 0.6f : 1f);
                if (HasVoice && Random.value < 0.3f) Sfx.Play(Sound.Yelp, 0.6f, VoicePitch);
            }
            else if (hp > Hp + 0.01f)
            {
                hitTime = 0f;
                SetTint(HealTint);
            }
            Hp = Mathf.Clamp(hp, 0.01f, Stats.maxHp); // only the host's "fallen" message ends it
            UpdateHpBar();

            for (; AttackCount < attacks; AttackCount++)
            {
                lungeTime = 0f;
                rig?.Attack();
                PlayAttackSound();
            }
        }

        /// <summary>Puts a freshly spawned puppet exactly where the host has it.</summary>
        public void SnapRemote(Vector3 position)
        {
            remoteTarget = position;
            transform.position = position;
        }

        /// <summary>The host says this unit has fallen: tear it apart here too.</summary>
        public void RemoteDie()
        {
            if (IsAlive) Die();
        }

        void RemoteTick()
        {
            // glide toward the host's position (it arrives ten times a second)
            var p = Vector3.Lerp(transform.position, remoteTarget, 1f - Mathf.Exp(-Time.deltaTime * 12f));
            transform.position = p;
            if (shadow != null) shadow.localPosition = new Vector3(0f, -p.y, 0f);
            Animate(remoteMoving);
        }
    }

    /// <summary>A torn-off stroke that tumbles, then shrinks away.</summary>
    public class PaperScrap : MonoBehaviour
    {
        const float Life = 1.6f;
        const float ShrinkTime = 0.5f;
        float age;
        Vector3 startScale;

        void Start() => startScale = transform.localScale;

        void Update()
        {
            age += Time.deltaTime;
            float k = (age - Life) / ShrinkTime;
            if (k >= 1f) Destroy(gameObject);
            else if (k > 0f) transform.localScale = startScale * (1f - k);
        }

        void OnDestroy() => DoodleBuilder.DestroyMeshes(gameObject);
    }
}
