using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArtWar
{
    public enum Phase { Draw, War, Over }

    /// <summary>
    /// Prototype bootstrap: builds the paper battlefield, the drawing UI and both castles.
    /// A match has a drawing phase (both sides draw their army with the same ink budget, units wait in formation)
    /// followed by the war. Press Play in any scene.
    /// </summary>
    public partial class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        [Header("Drawing phase")]
        public float drawPhaseSeconds = 120f;
        public float armyInk = 1000f;

        [Header("Battlefield")]
        public float fieldHalfX = 13f;
        [Tooltip("Extra distance each army stands back from the middle while waiting, so the two sides start far apart.")]
        public float startGap = 2.5f;
        public float fieldHalfZ = 3.4f;
        public float baseHp = 1500f;
        [Tooltip("Ink regained per second during the battle, for both sides.")]
        public int maxUnitsPerSide = 30;
        [Tooltip("Ink per second both sides slowly regain during the battle, so nobody gets stuck without ink")]
        public float warInkRegen = 4f;

        public Phase Phase { get; private set; } = Phase.Draw;
        public bool IsWar => Phase == Phase.War;

        const float UiColumnWidth = 660f; // reference pixels at 1080 height
        const float CameraPitch = 25f;

        readonly List<DoodleUnit> units = new List<DoodleUnit>();
        readonly Dictionary<(Team, int), int> formationCounts = new Dictionary<(Team, int), int>();
        readonly Queue<Doodle> enemyPlan = new Queue<Doodle>();
        DoodleUnit playerBase, enemyBase;
        float ink;
        float enemyInk; // what the enemy has left to draw reinforcements with
        float phaseTimer;
        float enemyDrawInterval;
        float enemyDrawTimer;

        Camera cam;
        DrawingPad pad;
        TMP_Text kindText, hintText, statsText, inkText, toastText, phaseText, countText, assistLabel;
        RectTransform inkFill;
        GameObject startButton;
        float toastUntil;

        // Prototype convenience: pressing Play in any scene (and reloading it) starts the battle.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureManager();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureManager();

        static void EnsureManager()
        {
            if (FindAnyObjectByType<BattleManager>() == null)
                new GameObject("Art War").AddComponent<BattleManager>();
        }

        void Awake()
        {
            Instance = this;
            Sfx.Ensure();
            Sfx.SetBattleMood(false);
            Sfx.PlayMusic(); // the last game may have ended with the music stopped
            ink = armyInk;
            LoadSettings();
            phaseTimer = drawPhaseSeconds;
            BuildWorld();
            BuildUI();
            SpawnBases();
            PlanEnemyArmy();
            RefreshStats();
            BuildMenus();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            UpdateView();
            FitCamera();
            if (Time.time > toastUntil) toastText.text = "";
            RefreshInkBar();

            switch (Phase)
            {
                case Phase.Draw:
                    phaseTimer -= Time.deltaTime;
                    enemyDrawTimer -= Time.deltaTime;
                    if (enemyDrawTimer <= 0f && enemyPlan.Count > 0)
                    {
                        enemyDrawTimer = enemyDrawInterval;
                        SpawnEnemyUnit(enemyPlan.Dequeue());
                    }
                    phaseText.text = Loc.T("CHUẨN BỊ", "GET READY") + "  " + FormatTime(phaseTimer);
                    if (phaseTimer <= 0f) StartWar();
                    break;

                case Phase.War:
                    // Both sides keep drawing during the battle: mostly with ink won by defeating enemies, plus a slow
                    // trickle so neither side is ever left unable to draw.
                    ink = Mathf.Min(armyInk, ink + warInkRegen * Time.deltaTime);
                    enemyInk = Mathf.Min(armyInk, enemyInk + warInkRegen * EnemyRegenFactor * Time.deltaTime);
                    enemyDrawTimer -= Time.deltaTime;
                    if (enemyDrawTimer <= 0f) EnemyReinforce();
                    phaseText.text = Loc.T("GIAO CHIẾN!  Phá lâu đài địch!", "BATTLE!  Destroy the enemy castle!");
                    break;
            }

            RefreshHud();
            UpdateMenus();
        }

        /// <summary>During the battle the enemy keeps drawing: a random unit it can afford every few seconds.</summary>
        void EnemyReinforce()
        {
            enemyDrawTimer = Random.Range(5f, 8f);
            if (EnemyTryUpgrade()) return;
            if (CountUnits(Team.Enemy) >= maxUnitsPerSide) return;
            var samples = DoodleTemplates.Samples;
            for (int tries = 0; tries < 6; tries++)
            {
                var d = samples[Random.Range(0, samples.Length)].make((InkColor)Random.Range(0, 3));
                float cost = d.TotalLength * DoodleAnalyzer.InkPerUnit;
                if (cost > enemyInk) continue;
                enemyInk -= cost;
                SpawnEnemyUnit(d);
                return;
            }
        }

        /// <summary>The whole army yells "yaaaa!" as the battle starts: a ragged chorus of little voices.</summary>
        System.Collections.IEnumerator BattleCry()
        {
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(Random.Range(0.08f, 0.2f));
                Sfx.Play(Sound.WarCry, 0.8f, Random.Range(0.85f, 1.15f));
            }
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        // ---------------------------------------------------------------- phases

        void StartWar()
        {
            if (Phase != Phase.Draw) return;
            while (enemyPlan.Count > 0) SpawnEnemyUnit(enemyPlan.Dequeue()); // the enemy finishes its army
            Phase = Phase.War;
            Sfx.Play(Sound.Fanfare, 0.7f);
            StartCoroutine(BattleCry());
            Sfx.SetBattleMood(true);
            warStartTime = Time.time;
            enemyDrawTimer = Random.Range(6f, 9f);
            startButton.SetActive(false);
            // the page stays open: keep drawing reinforcements; the only way to win is to bring down the enemy castle
        }

        void EndGame(Team? winner)
        {
            if (Phase == Phase.Over) return;
            Phase = Phase.Over;
            phaseText.text = Loc.T("KẾT THÚC", "GAME OVER");
            ShowResults(winner);
            Sfx.StopMusic();
            Sfx.Play(winner == Team.Enemy ? Sound.Defeat : Sound.Victory, 0.8f);
        }

        // ---------------------------------------------------------------- units

        DoodleUnit SpawnUnit(Doodle doodle, Team team, Vector3 position, UnitStats stats, bool isBase = false, float scale = 1f)
        {
            var go = new GameObject(isBase ? team + " Castle" : team + " " + stats.kind);
            go.transform.position = position;
            var unit = go.AddComponent<DoodleUnit>();
            unit.Init(doodle, team, stats, isBase, scale);
            units.Add(unit);
            return unit;
        }

        /// <summary>
        /// Fields one drawing. Soldiers come in numbers: the better drawn (<paramref name="quality"/>, 0..1), the more
        /// of them, a bit smaller than drawn. Big units are always one. Never more than three.
        /// </summary>
        int SpawnArmyUnit(Doodle doodle, Team team, float quality)
        {
            var stats = DoodleAnalyzer.Analyze(doodle);
            int copies = DoodleQuality.Copies(doodle, stats, quality);
            int count = copies;
            float scale = count >= 3 ? 0.78f : count == 2 ? 0.88f : 1f;
            stats.copies = count;
            // a squad shares the drawing's strength: each copy is weaker, the group still stronger than one
            float share = count >= 3 ? 0.55f : count == 2 ? 0.7f : 1f;
            stats.maxHp *= share;
            stats.damage *= share;
            int made = 0;
            for (int i = 0; i < count && (made == 0 || CountUnits(team) < maxUnitsPerSide); i++, made++)
                SpawnUnit(doodle, team, FormationSlot(team, stats.kind), stats, false, scale);
            return made;
        }

        /// <summary>The enemy's drawings are of mixed quality.</summary>
        void SpawnEnemyUnit(Doodle doodle) => SpawnArmyUnit(doodle, Team.Enemy, EnemyDrawingQuality());

        /// <summary>Lines the army up while waiting: walls in front, fighters, then tanks/fliers, healers and shooters at the back.</summary>
        Vector3 FormationSlot(Team team, UnitKind kind)
        {
            int group;
            float depth;
            switch (kind)
            {
                case UnitKind.Wall: group = 0; depth = 1.3f; break;
                case UnitKind.Shieldman: group = 7; depth = 1.9f; break; // the phalanx holds the front
                case UnitKind.Infantry:
                case UnitKind.Cavalry:
                case UnitKind.Robot:
                case UnitKind.Spider: group = 1; depth = 2.5f; break;
                case UnitKind.Tank: group = 2; depth = 3.8f; break;
                case UnitKind.Plane:
                case UnitKind.Jet:
                case UnitKind.Helicopter:
                case UnitKind.Dragon:
                case UnitKind.Ghost:
                case UnitKind.Bat:
                case UnitKind.Balloon: group = 3; depth = 3.0f; break;
                case UnitKind.Healer: group = 4; depth = 6.1f; break;
                case UnitKind.Rifleman: group = 6; depth = 5.0f; break;
                default: group = 5; depth = 6.9f; break; // archers, turrets
            }
            formationCounts.TryGetValue((team, group), out int i);
            formationCounts[(team, group)] = i + 1;

            float[] lanes = { 0f, -1.5f, 1.5f, -0.75f, 0.75f, -2.2f, 2.2f };
            float z = Mathf.Clamp(lanes[i % lanes.Length] * fieldHalfZ / 2.2f, -fieldHalfZ, fieldHalfZ); // lanes spread over the lane's depth
            depth = Mathf.Min(startGap + depth + (i / lanes.Length) * 1.1f, fieldHalfX - 0.5f);
            float side = team == Team.Player ? -1f : 1f;
            return new Vector3(side * depth, 0f, z);
        }

        void SpawnBases()
        {
            var stats = new UnitStats { maxHp = baseHp, attackInterval = 1f };
            playerBase = SpawnUnit(DoodleTemplates.Castle(InkColor.Blue), Team.Player,
                new Vector3(-fieldHalfX - 1.4f, 0f, 0f), stats, true, 1.3f);
            enemyBase = SpawnUnit(DoodleTemplates.Castle(InkColor.Red), Team.Enemy,
                new Vector3(fieldHalfX + 1.4f, 0f, 0f), stats, true, 1.3f);
        }

        /// <summary>The enemy "draws" with the same ink budget: pick random sample drawings until the ink runs out.</summary>
        void PlanEnemyArmy()
        {
            float budget = armyInk * EnemyBudgetFactor;
            var samples = DoodleTemplates.Samples;
            for (int tries = 0; tries < 200 && enemyPlan.Count < maxUnitsPerSide; tries++)
            {
                var d = samples[Random.Range(0, samples.Length)].make((InkColor)Random.Range(0, 3));
                float cost = d.TotalLength * DoodleAnalyzer.InkPerUnit;
                if (cost > budget) continue;
                budget -= cost;
                enemyPlan.Enqueue(d);
                if (budget < 50f) break;
            }
            enemyInk = budget; // the rest is saved for reinforcements during the battle
            enemyDrawInterval = drawPhaseSeconds * 0.85f / Mathf.Max(1, enemyPlan.Count);
            enemyDrawTimer = enemyDrawInterval;
        }

        void OnSummonClicked()
        {
            if (Phase == Phase.Over) return;
            if (pad.CurrentDoodle.TotalLength < 0.08f)
            {
                Toast(Loc.T("Vẽ thêm chút nữa đã!", "Draw a bit more first!"));
                return;
            }
            if (CountUnits(Team.Player) >= maxUnitsPerSide)
            {
                Toast(Loc.T("Quân đã đầy!", "Your army is full!"));
                return;
            }
            var doodle = pad.TakeDoodle();
            var stats = DoodleAnalyzer.Analyze(doodle);
            float quality = DoodleQuality.Score(doodle, stats);
            int made = SpawnArmyUnit(doodle, Team.Player, quality);
            statDrawn += made;
            if (DoodleQuality.Multiplies(stats.kind) && !doodle.readyMade)
            {
                // praise the drawing: the stars it earned and how many soldiers came out of it
                Sfx.Play(Sound.Twinkle, 0.6f);
                toastText.text = $"{DoodleQuality.StarText(DoodleQuality.Stars(quality))}  ×{made} {DoodleAnalyzer.DisplayName(stats)}!";
                toastUntil = Time.time + 1.8f;
            }
        }

        void OnSampleClicked(Doodle sample)
        {
            if (Phase == Phase.Over) return;
            if (!pad.Load(sample)) Toast(Loc.T("Không đủ mực!", "Not enough ink!"));
        }

        void OnAssistClicked()
        {
            pad.Assist = !pad.Assist;
            assistLabel.text = pad.Assist ? Loc.T("Hỗ trợ\nBẬT", "Assist\nON") : Loc.T("Hỗ trợ\nTẮT", "Assist\nOFF");
        }

        int CountUnits(Team team)
        {
            int n = 0;
            foreach (var u in units)
                if (u.Team == team && !u.IsBase && u.IsAlive) n++;
            return n;
        }

        /// <summary>Nearest enemy this unit is able to hit; falls back to the enemy castle.</summary>
        public DoodleUnit FindTarget(DoodleUnit self)
        {
            DoodleUnit best = null;
            float bestDist = float.MaxValue;
            foreach (var u in units)
            {
                if (u.Team == self.Team || u.IsBase || !u.IsAlive || !self.CanHit(u)) continue;
                float d = FlatDistanceSq(u, self);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = u;
                }
            }
            if (best != null) return best;
            var enemyCastle = self.Team == Team.Player ? enemyBase : playerBase;
            return enemyCastle != null && enemyCastle.IsAlive ? enemyCastle : null;
        }

        /// <summary>Nearest living enemy unit (ground or air) within range, for castle archers.</summary>
        /// <summary>Enemies within range, nearest first (a castle's volley picks several).</summary>
        public List<DoodleUnit> FindEnemies(DoodleUnit self, float range, bool groundOnly = false)
        {
            var found = new List<DoodleUnit>();
            float r2 = range * range;
            foreach (var u in units)
            {
                if (u.Team == self.Team || u.IsBase || !u.IsAlive || (groundOnly && u.IsFlying)) continue;
                if (FlatDistanceSq(u, self) < r2) found.Add(u);
            }
            found.Sort((a, b) => FlatDistanceSq(a, self).CompareTo(FlatDistanceSq(b, self)));
            return found;
        }

        public DoodleUnit FindNearestEnemy(DoodleUnit self, float range)
        {
            DoodleUnit best = null;
            float bestDist = range * range;
            foreach (var u in units)
            {
                if (u.Team == self.Team || u.IsBase || !u.IsAlive) continue;
                float d = FlatDistanceSq(u, self);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = u;
                }
            }
            return best;
        }

        /// <summary>The most hurt ally (lowest health ratio) for a healer.</summary>
        public DoodleUnit FindHurtAlly(DoodleUnit healer)
        {
            DoodleUnit best = null;
            float bestRatio = 0.98f;
            foreach (var u in units)
            {
                if (u == healer || u.Team != healer.Team || u.IsBase || !u.IsAlive) continue;
                float ratio = u.Hp / u.Stats.maxHp;
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best = u;
                }
            }
            return best;
        }

        public DoodleUnit FindNearestAlly(DoodleUnit self)
        {
            DoodleUnit best = null;
            float bestDist = float.MaxValue;
            foreach (var u in units)
            {
                if (u == self || u.Team != self.Team || u.IsBase || !u.IsAlive || u.Kind == UnitKind.Healer) continue;
                float d = FlatDistanceSq(u, self);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = u;
                }
            }
            return best;
        }

        static float FlatDistanceSq(DoodleUnit a, DoodleUnit b)
        {
            Vector3 d = a.transform.position - b.transform.position;
            d.y = 0f;
            return d.sqrMagnitude;
        }

        /// <summary>Pushes allies at the same height apart so they don't stack into one blob.</summary>
        public Vector3 Separation(DoodleUnit self)
        {
            Vector3 push = Vector3.zero;
            foreach (var u in units)
            {
                if (u == self || u.Team != self.Team || u.IsBase || !u.IsAlive || u.IsFlying != self.IsFlying) continue;
                Vector3 d = self.transform.position - u.transform.position;
                d.y = 0f;
                float min = (self.Radius + u.Radius) * 0.7f;
                float m = d.magnitude;
                if (m >= min) continue;
                if (m < 0.001f) d = new Vector3(0f, 0f, Random.Range(-1f, 1f));
                push += d.normalized * (min - m) * 3f;
            }
            return push;
        }

        public Vector3 ClampToField(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -fieldHalfX - 2f, fieldHalfX + 2f);
            p.z = Mathf.Clamp(p.z, -fieldHalfZ, fieldHalfZ);
            return p;
        }

        /// <summary>Explosion on the ground (shell, bomb): hurts every enemy the attacker can hit within the radius.</summary>
        public void DamageArea(DoodleUnit attacker, Vector3 point, float radius, float damage, bool hitsAir)
        {
            Team team = attacker.Team;
            foreach (var u in units.ToArray())
            {
                if (u.Team == team || !u.IsAlive || (u.IsFlying && !hitsAir)) continue;
                Vector3 d = u.transform.position - point;
                d.y = 0f;
                if (d.magnitude <= radius + u.Radius)
                    u.TakeDamage(damage * attacker.Counter(u));
            }
        }

        /// <summary>Dragon fire: hurts every enemy (ground or air) in front of the dragon within range.</summary>
        public void Breath(DoodleUnit dragon, float range, float damage)
        {
            Vector3 origin = dragon.transform.position;
            foreach (var u in units.ToArray())
            {
                if (u.Team == dragon.Team || !u.IsAlive) continue;
                Vector3 d = u.transform.position - origin;
                d.y = 0f;
                bool inFront = d.x * dragon.Facing > -0.3f;
                if (inFront && d.magnitude <= range + u.Radius + dragon.Radius)
                    u.TakeDamage(damage * dragon.Counter(u));
            }
        }

        public void OnUnitDied(DoodleUnit unit)
        {
            units.Remove(unit);
            if (unit.IsBase)
            {
                EndGame(unit.Team == Team.Enemy ? Team.Player : Team.Enemy);
                return;
            }
            if (Phase != Phase.War) return;

            // Ink is won by defeating enemies: a share of what the fallen unit's drawing cost, split between its copies.
            float reward = KillReward(unit);
            if (unit.Team == Team.Enemy)
            {
                ink = Mathf.Min(armyInk, ink + reward);
                statKilled++;
                statInkEarned += reward;
                inkGain = Time.time < inkGainUntil ? inkGain + reward : reward;
                inkGainUntil = Time.time + 1.2f;
            }
            else
            {
                enemyInk = Mathf.Min(armyInk, enemyInk + reward);
                statLost++;
            }

            // the winners giggle now and then, "hi-hi-hi!"
            if (Random.value < 0.35f) Sfx.Play(Sound.Giggle, 0.75f, Random.Range(0.9f, 1.1f));
        }

        public static float KillReward(DoodleUnit unit) =>
            Mathf.Round(15f + unit.Stats.inkCost * 0.4f / Mathf.Max(1, unit.Stats.copies));

        // ---------------------------------------------------------------- castle upgrades

        float inkGain, inkGainUntil;
        TMP_Text upgradeLabel;
        Image upgradeImage;

        void OnUpgradeClicked()
        {
            if (Phase == Phase.Over || playerBase == null) return;
            int next = playerBase.CastleLevel + 1;
            if (next > DoodleUnit.CastleMaxLevel)
            {
                Toast(Loc.T("Thành đã cấp tối đa!", "The castle is at max level!"));
                return;
            }
            float cost = DoodleUnit.UpgradeCost(next);
            if (ink < cost)
            {
                Toast(Loc.T($"Cần {cost:0} mực để nâng cấp!", $"Need {cost:0} ink to upgrade!"));
                return;
            }
            ink -= cost;
            playerBase.UpgradeCastle();
            toastText.text = Loc.T($"Thành lên cấp {next}!", $"Castle up to level {next}!");
            toastUntil = Time.time + 1.5f;
        }

        /// <summary>The enemy reinforces its castle when it can afford it, more eagerly once its walls are hurt.</summary>
        bool EnemyTryUpgrade()
        {
            if (enemyBase == null || enemyBase.CastleLevel >= DoodleUnit.CastleMaxLevel) return false;
            float cost = DoodleUnit.UpgradeCost(enemyBase.CastleLevel + 1);
            bool hurt = enemyBase.Hp < enemyBase.Stats.maxHp * 0.6f;
            if (enemyInk < cost || (!hurt && Random.value > 0.3f)) return false;
            enemyInk -= cost;
            enemyBase.UpgradeCastle();
            return true;
        }

        void RefreshUpgradeButton()
        {
            if (upgradeLabel == null || playerBase == null) return;
            int next = playerBase.CastleLevel + 1;
            if (next > DoodleUnit.CastleMaxLevel)
            {
                upgradeLabel.text = Loc.T($"THÀNH CẤP {DoodleUnit.CastleMaxLevel} (tối đa)", $"CASTLE LEVEL {DoodleUnit.CastleMaxLevel} (max)");
                upgradeImage.color = new Color(0.45f, 0.42f, 0.38f);
                return;
            }
            float cost = DoodleUnit.UpgradeCost(next);
            upgradeLabel.text = Loc.T($"NÂNG CẤP THÀNH → cấp {next}  ({cost:0} mực)", $"UPGRADE CASTLE → level {next}  ({cost:0} ink)") +
                                $"\n<size=68%>{DoodleUnit.UpgradeInfo(next)}</size>";
            upgradeImage.color = ink >= cost ? new Color(0.3f, 0.45f, 0.75f) : new Color(0.35f, 0.35f, 0.42f);
        }

        // ---------------------------------------------------------------- ink

        bool TryConsumeInk(float amount)
        {
            if (Phase == Phase.Over || ink < amount) return false;
            ink -= amount;
            return true;
        }

        void RefundInk(float amount) => ink = Mathf.Min(armyInk, ink + amount);

        void RefreshInkBar()
        {
            inkFill.anchorMax = new Vector2(ink / armyInk, 1f);
            inkText.text = $"{Loc.T("Mực", "Ink")}  {Mathf.FloorToInt(ink)} / {armyInk:0}" +
                           (Time.time < inkGainUntil ? $"   <color=#A8FFA0>+{inkGain:0} {Loc.T("hạ địch!", "defeated!")}</color>" : "");
            RefreshUpgradeButton();
        }

        void RefreshStats()
        {
            var d = pad.CurrentDoodle;
            var s = DoodleAnalyzer.Analyze(d);

            if (d.TotalLength < 0.05f)
            {
                kindText.text = Loc.T("Vẽ một chiến binh!", "Draw a warrior!");
                kindText.color = Color.white;
                hintText.text = Loc.T("Game nhìn hình để biết đó là lính gì. Cách vẽ từng loại: xem Sổ tay quân ở màn hình chính", "The game reads your drawing to tell which unit it is. How to draw each one: see the Unit Handbook on the title screen");
                statsText.text = Loc.T("Vẽ xong bấm <b>Triệu hồi!</b>\n", "When done, press <b>Summon!</b>\n") +
                                 Loc.T("<size=75%>Viền màu: chân (xanh lá) · vũ khí (đỏ) · súng (cam) · khiên (xanh dương) · cánh (lơ) · bánh (vàng)</size>", "<size=75%>Outlines: legs (green) · weapon (red) · gun (orange) · shield (blue) · wings (cyan) · wheels (yellow)</size>");
                return;
            }
            kindText.text = DoodleAnalyzer.DisplayName(s);
            kindText.color = new Color(1f, 0.85f, 0.35f);
            hintText.text = DoodleAnalyzer.KindHint(s.kind);
            string range = s.range > 0f ? $"   {Loc.T("Tầm", "Range")} {s.range:0.#}" : "";
            string power = s.kind == UnitKind.Healer ? $"{Loc.T("Hồi máu", "Heals")} {s.damage:0}" :
                s.kind == UnitKind.Wall ? Loc.T("Không tấn công", "No attack") : $"{Loc.T("Sát thương", "Damage")} {s.damage:0.#}";
            string beauty;
            if (!DoodleQuality.Multiplies(s.kind))
                beauty = Loc.T("Đơn vị mạnh: luôn ra 1 con", "Strong unit: always just one");
            else if (d.readyMade)
                beauty = Loc.T("Hình vẽ hộ: ra ×1 (tự vẽ đẹp để được nhiều lính hơn)", "Ready-made drawing: ×1 (draw it yourself, nicely, for more soldiers)");
            else
            {
                float q = DoodleQuality.Score(d, s);
                int copies = DoodleQuality.Copies(d, s, q);
                string stars = $"<color=#FFD84A>{DoodleQuality.StarText(DoodleQuality.Stars(q))}</color> {q * 100f:0}%";
                beauty = Loc.T($"Độ đẹp {stars}  →  ra ×{copies} lính", $"Beauty {stars}  →  ×{copies} soldiers");
            }
            statsText.text = $"{Loc.T("Máu", "HP")} {s.maxHp:0}   {power}   {Loc.T("Tốc độ", "Speed")} {s.speed:0.0}{range}   " +
                             $"{Loc.T("Mực", "Ink")} {pad.InkUsed:0}\n" + beauty;
        }

        void Toast(string message)
        {
            Sfx.Play(Sound.Error, 0.5f);
            toastText.text = message;
            toastUntil = Time.time + 1.5f;
        }

        // ---------------------------------------------------------------- world

        void BuildWorld()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Scenery.SkyColor;
            cam.fieldOfView = 40f;
            // The scene's post-processing (tonemapping, grading) washes out the crayon colors.
            var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = false;
            FitCamera();

            // The paper battlefield and the landscape never change, so they are built once and kept across scene
            // reloads (Play Again, Main Menu, language switch) instead of being painted again every time.
            if (world != null) return;
            world = new GameObject("World");
            DontDestroyOnLoad(world);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Paper Ground";
            ground.transform.SetParent(world.transform, false);
            // the page reaches past both castles, and from just behind the lane forward to the river bank
            float paperWidth = (fieldHalfX + 3f) * 2f;
            float paperFront = Waterscape.RiverZ + 2.3f, paperBack = fieldHalfZ + 2.6f;
            float paperDepth = paperBack - paperFront, paperCentre = (paperBack + paperFront) * 0.5f;
            ground.transform.position = new Vector3(0f, 0f, paperCentre);
            ground.transform.localScale = new Vector3(paperWidth * 0.1f, 1f, paperDepth * 0.1f);
            ground.GetComponent<Renderer>().sharedMaterial =
                DoodleBuilder.TextureMaterial(PaperGround.Paint(paperWidth, paperDepth, paperCentre, fieldHalfX, fieldHalfZ));
            Scenery.Build(world.transform, fieldHalfX, fieldHalfZ);
        }

        static GameObject world;

        /// <summary>Frames the whole battlefield in the screen area to the right of the drawing column.</summary>
        void FitCamera()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float uiFraction = Mathf.Clamp(UiColumnWidth / (1080f * aspect), 0f, 0.6f);
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float fieldWidth = (fieldHalfX + 2.6f) * 2f;
            float distance = Mathf.Clamp(fieldWidth / (2f * (1f - uiFraction) * 0.95f * tanHalf * aspect), 10f, 40f) * viewZoom;
            float halfWidth = distance * tanHalf * aspect;

            var rot = Quaternion.Euler(CameraPitch, 0f, 0f);
            Vector3 focus = new Vector3(-uiFraction * halfWidth + viewPan.x, 1.2f, viewPan.y);
            cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
        }

        // ---------------------------------------------------------------- UI

        void BuildUI()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            // The HUD comes from Resources/UI/HUD.prefab (edit it there); without the prefab it is laid out in code.
            var prefab = Resources.Load<HudView>("UI/HUD");
            hud = prefab != null ? Instantiate(prefab) : HudView.Build();
            hud.name = "HUD";

            kindText = hud.kindText;
            hintText = hud.hintText;
            statsText = hud.statsText;
            inkText = hud.inkText;
            inkFill = hud.inkFill;
            toastText = hud.toastText;
            phaseText = hud.phaseText;
            countText = hud.countText;
            assistLabel = hud.assistLabel;
            startButton = hud.startButton.gameObject;
            upgradeLabel = hud.upgradeLabel;
            upgradeImage = hud.upgradeButton.GetComponent<Image>();

            pad = hud.pad.gameObject.AddComponent<DrawingPad>();
            pad.TryConsumeInk = TryConsumeInk;
            pad.RefundInk = RefundInk;
            pad.Changed += RefreshStats;

            for (int i = 0; i < hud.colorButtons.Length; i++)
            {
                var color = (InkColor)i;
                OnClick(hud.colorButtons[i], () => pad.InkColor = color);
            }
            OnClick(hud.undoButton, pad.Undo);
            OnClick(hud.clearButton, () => pad.Clear(true));
            OnClick(hud.assistButton, OnAssistClicked);
            OnClick(hud.summonButton, OnSummonClicked);
            OnClick(hud.startButton, StartWar);
            OnClick(hud.upgradeButton, OnUpgradeClicked);
            OnClick(hud.pauseButton, () => ShowPage(Page.Pause));

            RefreshInkBar();
        }

        HudView hud;

        /// <summary>Every button clicks, then does its job.</summary>
        static void OnClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(() => Sfx.Play(Sound.Click, 0.5f));
            button.onClick.AddListener(action);
        }
    }
}
