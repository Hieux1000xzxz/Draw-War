using System.Collections.Generic;
using UnityEngine;

namespace ArtWar
{
    /// <summary>
    /// Brings a built drawing to life part by part: legs step in turn, wings flap, rotors and propellers spin,
    /// wheels roll with the distance travelled, weapons wind up and strike (or recoil for guns), bodies breathe,
    /// heads bob, and the eyes follow the target, blink and squint when hit.
    /// </summary>
    public class DoodleRig
    {
        class Joint
        {
            public Transform t;
            public PartType type;
            public bool isHead;
            public bool closed;
            public Vector3 rest;
            public float radius;
            public float phase;
            public bool backLeg;   // the rear leg drops to the knee when kneeling
            public bool isArm;     // robot arm, hanging from its top edge
            public float armPhase;
            public Vector2 tip;
        }

        /// <summary>0 = standing, 1 = down on one knee to shoot.</summary>
        public float Kneel { get; set; }

        /// <summary>Where the gun should point, in the drawing's local space (x forward = +facing).</summary>
        public Vector2 Aim { get; set; } = new Vector2(1f, 0.15f);

        readonly List<Joint> joints = new List<Joint>();
        readonly List<Transform> eyes;
        readonly List<Transform> pupils;
        readonly float eyeRadius;
        readonly UnitKind kind;
        readonly RobotMode robotMode;
        readonly float facing;     // +1 drawn facing right, -1 mirrored
        readonly float seed;

        float wheelAngle;
        float attackTime = -1f;
        float hitTime = -1f;
        float blinkTimer;
        float blinkTime = -1f;

        const float AttackDuration = 0.45f;

        bool Melee => kind == UnitKind.Infantry || kind == UnitKind.Cavalry || kind == UnitKind.Dragon || kind == UnitKind.Bat ||
                      (kind == UnitKind.Robot && robotMode == RobotMode.Sword);

        public DoodleRig(BuiltDoodle built, UnitStats stats)
        {
            kind = stats.kind;
            robotMode = stats.robotMode;
            facing = built.mirrored ? -1f : 1f;
            eyes = built.eyes;
            pupils = built.pupils;
            eyeRadius = built.eyeRadius;
            seed = Random.value * 10f;
            blinkTimer = Random.Range(1.5f, 4f);

            // Legs alternate front-to-back so four-legged drawings get a diagonal gait.
            var legs = built.parts.FindAll(p => p.type == PartType.Leg);
            legs.Sort((a, b) => a.restPosition.x.CompareTo(b.restPosition.x));

            // Robot arms: boxes beside the body (not the head, not the biggest box which is the body).
            BuiltPart bodyBox = null;
            foreach (var p in built.parts)
                if (p.type == PartType.Box && (bodyBox == null || p.bounds.width * p.bounds.height > bodyBox.bounds.width * bodyBox.bounds.height))
                    bodyBox = p;
            int armIndex = 0;

            foreach (var p in built.parts)
            {
                if (p.attached && !p.held) continue; // rides along with its wing, leg or barrel
                bool arm = kind == UnitKind.Robot && p.type == PartType.Box && p != bodyBox && !p.isHead;
                joints.Add(new Joint
                {
                    t = p.go.transform,
                    type = p.type,
                    isHead = p.isHead,
                    closed = p.closed,
                    rest = p.restPosition,
                    radius = Mathf.Max(p.radius, 0.05f),
                    phase = p.type == PartType.Leg ? (legs.IndexOf(p) % 2) * Mathf.PI : Random.value * 6f,
                    backLeg = legs.Count > 0 && p == (facing > 0f ? legs[0] : legs[legs.Count - 1]),
                    tip = p.tip,
                    isArm = arm,
                    armPhase = arm ? (armIndex++ % 2) * Mathf.PI : 0f,
                });
            }
        }

        public void Attack() => attackTime = 0f;
        public void Hit() => hitTime = 0f;

        /// <summary>World position of the muzzle, if the drawing holds a gun.</summary>
        public bool TryGetMuzzle(out Vector3 world)
        {
            foreach (var j in joints)
            {
                if (j.type != PartType.Gun) continue;
                world = j.t.TransformPoint(j.tip);
                return true;
            }
            world = Vector3.zero;
            return false;
        }

        /// <param name="walkPhase">step cycle of the unit</param>
        /// <param name="travelX">world distance moved along x this frame (for wheels)</param>
        /// <param name="look">direction to look at, in the drawing's local space</param>
        public void Tick(float dt, bool moving, bool airborne, float walkPhase, float travelX, Vector2 look)
        {
            float time = Time.time + seed;
            wheelAngle -= travelX / 0.1f * Mathf.Rad2Deg; // radius applied per wheel below

            float attack = 0f, recoil = 0f, smash = 0f;
            if (attackTime >= 0f)
            {
                attackTime += dt;
                float k = attackTime / AttackDuration;
                if (k >= 1f) attackTime = -1f;
                else
                {
                    // robot ground pound: raise the arms overhead, slam them down, ease back
                    smash = k < 0.45f ? Mathf.SmoothStep(0f, 150f, k / 0.45f)
                        : k < 0.6f ? Mathf.Lerp(150f, -10f, (k - 0.45f) / 0.15f)
                        : Mathf.Lerp(-10f, 0f, (k - 0.6f) / 0.4f);
                    // wind up, strike, recover
                    attack = k < 0.35f ? -0.35f * Mathf.Sin(k / 0.35f * Mathf.PI * 0.5f)
                        : k < 0.55f ? Mathf.Lerp(-0.35f, 1f, (k - 0.35f) / 0.2f)
                        : 1f - (k - 0.55f) / 0.45f;
                    recoil = k < 0.12f ? k / 0.12f : 1f - (k - 0.12f) / 0.88f;
                }
            }

            foreach (var j in joints)
            {
                Vector3 pos = j.rest;
                Quaternion rot = Quaternion.identity;
                Vector3 scale = Vector3.one;

                switch (j.type)
                {
                    case PartType.Leg:
                        if (moving && !airborne)
                            rot = Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase + j.phase) * 28f);
                        else if (airborne)
                            rot = Quaternion.Euler(0f, 0f, -18f * facing + Mathf.Sin(time * 3f + j.phase) * 6f); // tucked legs
                        else if (Kneel > 0f)
                            // rear leg folds back onto the knee, front leg braces forward
                            rot = Quaternion.Euler(0f, 0f, (j.backLeg ? -35f : 12f) * facing * Kneel);
                        break;

                    case PartType.Gun:
                    {
                        // shoulder the rifle and track the target; kick back on every shot
                        float angle = Mathf.Clamp(Mathf.Atan2(Aim.y, Mathf.Abs(Aim.x)) * Mathf.Rad2Deg, -20f, 25f);
                        rot = Quaternion.Euler(0f, 0f, (angle + recoil * 6f) * facing);
                        pos -= new Vector3(facing * 0.07f * recoil, 0f, 0f);
                        break;
                    }

                    case PartType.Weapon:
                        if (Melee)
                            rot = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.8f) * 3f - attack * 35f * facing);
                        else
                            pos -= new Vector3(facing * 0.15f * recoil, 0f, 0f);
                        break;

                    case PartType.Bow:
                        scale = new Vector3(1f - 0.2f * recoil, 1f, 1f);
                        break;

                    case PartType.Wing:
                        if (kind == UnitKind.Bat)
                        {
                            // quick fluttery bat wingbeats
                            float beat = Mathf.Sin(time * 16f) * 32f;
                            rot = Quaternion.Euler(0f, 0f, beat * Mathf.Sign(j.tip.x == 0f ? 1f : -j.tip.x));
                        }
                        else if (kind == UnitKind.Dragon)
                        {
                            // big slow wingbeats, up and down around the shoulder: a wing reaching back
                            // lifts by turning clockwise, one reaching forward by turning counter-clockwise
                            float speed = airborne ? 5.5f : 2f, amount = airborne ? 28f : 8f;
                            float beat = Mathf.Sin(time * speed) * amount;
                            rot = Quaternion.Euler(beat * 0.25f, 0f, beat * Mathf.Sign(j.tip.x == 0f ? 1f : -j.tip.x));
                        }
                        else rot = Quaternion.Euler(Mathf.Sin(time * 2f + j.phase) * 4f, 0f, 0f); // rigid aircraft wing
                        break;

                    case PartType.Rotor:
                        rot = Quaternion.Euler(0f, time * 900f % 360f, 0f);
                        break;

                    case PartType.Propeller:
                        rot = Quaternion.Euler(time * 1400f % 360f, 0f, 0f);
                        break;

                    case PartType.Wheel:
                        rot = Quaternion.Euler(0f, 0f, wheelAngle * 0.1f / j.radius);
                        break;

                    case PartType.Cross:
                        scale = Vector3.one * (1f + 0.12f * Mathf.Sin(time * 4f));
                        break;

                    default:
                        if (j.isArm)
                        {
                            // swing in step while walking, raise and slam on attack
                            float swing = moving ? Mathf.Sin(walkPhase + j.armPhase) * 18f : Mathf.Sin(time * 1.3f + j.armPhase) * 3f;
                            // fists slam with both arms; a sword arm chops; gun and jet robots keep their arms steady
                            float strike = robotMode == RobotMode.Smash ? smash : robotMode == RobotMode.Sword ? smash * 0.6f : 0f;
                            rot = Quaternion.Euler(0f, 0f, (swing + strike) * facing);
                        }
                        else if (j.isHead)
                        {
                            float bob = moving && !airborne ? Mathf.Sin(walkPhase * 2f) * 0.035f : Mathf.Sin(time * 1.7f) * 0.015f;
                            pos += new Vector3(attack * 0.06f * facing, bob, 0f);
                            rot = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.1f) * 5f - attack * 8f * facing);
                        }
                        else if (j.closed)
                        {
                            scale = Vector3.one * (1f + 0.025f * Mathf.Sin(time * 2.4f + j.phase)); // breathing
                        }
                        break;
                }

                j.t.localPosition = pos;
                j.t.localRotation = rot;
                j.t.localScale = scale;
            }

            AnimateEyes(dt, look);
        }

        void AnimateEyes(float dt, Vector2 look)
        {
            if (eyes.Count == 0) return;

            blinkTimer -= dt;
            if (blinkTimer <= 0f)
            {
                blinkTimer = Random.Range(2f, 5f);
                blinkTime = 0f;
            }
            float open = 1f;
            if (blinkTime >= 0f)
            {
                blinkTime += dt;
                if (blinkTime > 0.14f) blinkTime = -1f;
                else open = 0.1f;
            }
            if (hitTime >= 0f)
            {
                hitTime += dt;
                if (hitTime > 0.3f) hitTime = -1f;
                else open = Mathf.Min(open, 0.3f); // squint
            }

            Vector2 offset = look.sqrMagnitude > 1e-4f ? look.normalized * eyeRadius * 0.4f : Vector2.zero;
            for (int i = 0; i < eyes.Count; i++)
            {
                eyes[i].localScale = new Vector3(1f, open, 1f);
                var p = pupils[i].localPosition;
                pupils[i].localPosition = new Vector3(offset.x, offset.y, p.z);
            }
        }
    }
}
