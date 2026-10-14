using System.Collections.Generic;
using UnityEngine;

namespace ArtWar
{
    public enum UnitKind { Infantry, Cavalry, Archer, Healer, Wall, Turret, Tank, Plane, Jet, Helicopter, Dragon, Rifleman, Robot, Shieldman, Ghost,
        Grenadier, Catapult, Bat }

    /// <summary>What a single stroke was recognized as.</summary>
    public enum PartType { None, Leg, Weapon, Shield, Wheel, Wing, Bow, Rotor, Propeller, Cross, Gun, Box, Guard, Bomb }

    /// <summary>The body the player chooses to draw; the weapon they add then decides the exact unit.</summary>
    public enum Model { Human, Robot, Dragon, Tank, Aircraft, Fort, Ghost }

    /// <summary>How a robot fights, from what it holds: nothing, a blade, a level barrel, or wings.</summary>
    public enum RobotMode { Smash, Sword, Cannon, Flyer }

    public struct UnitStats
    {
        public UnitKind kind;
        public RobotMode robotMode;
        public float maxHp;
        public float damage;  // healers: amount healed per tick
        public float speed;
        public float attackInterval;
        public float range;   // 0 = melee
        public float splash;  // 0 = single target
        public bool flying;
        public float inkCost;
        public float size;
        public int legs, weapons, shields, wheels, wings, bows, rotors, propellers, crosses, guns, boxes, guards, bombs;
        public int steepWeapons; // weapons raised steeply (a catapult arm rather than a level barrel)
        public int closedShapes; // closed outlines: body, head, tail, belly... (a real dragon has several)
        public float armor; // fraction of incoming damage blocked (shield bearers, upgraded castle walls)
        public int copies;  // how many soldiers the drawing became (kill rewards are shared between them)

        public bool HitsAir => kind == UnitKind.Archer || kind == UnitKind.Turret || kind == UnitKind.Dragon ||
                               kind == UnitKind.Jet || kind == UnitKind.Helicopter || kind == UnitKind.Rifleman ||
                               kind == UnitKind.Bat || (kind == UnitKind.Robot && robotMode == RobotMode.Flyer);

        public bool Stationary => kind == UnitKind.Wall || kind == UnitKind.Turret;
    }

    /// <summary>
    /// Reads the drawing's shape. Each stroke becomes a part (leg, weapon, shield, wheel, wing, bow, rotor,
    /// propeller, cross), the parts decide the unit kind, and size/ink/color tune the numbers.
    /// </summary>
    public static class DoodleAnalyzer
    {
        public const float InkPerUnit = 40f;

        public static string KindName(UnitKind k)
        {
            switch (k)
            {
                case UnitKind.Cavalry: return Loc.T("KỴ BINH", "KNIGHT");
                case UnitKind.Archer: return Loc.T("CUNG THỦ", "ARCHER");
                case UnitKind.Healer: return Loc.T("THẦY THUỐC", "MEDIC");
                case UnitKind.Wall: return Loc.T("TƯỜNG THÀNH", "WALL");
                case UnitKind.Turret: return Loc.T("THÁP CANH", "WATCHTOWER");
                case UnitKind.Tank: return Loc.T("XE TĂNG", "TANK");
                case UnitKind.Plane: return Loc.T("MÁY BAY CÁNH QUẠT", "PROP PLANE");
                case UnitKind.Jet: return Loc.T("MÁY BAY PHẢN LỰC", "JET");
                case UnitKind.Helicopter: return Loc.T("TRỰC THĂNG", "HELICOPTER");
                case UnitKind.Dragon: return Loc.T("RỒNG", "DRAGON");
                case UnitKind.Rifleman: return Loc.T("ĐỘI XẠ THỦ", "RIFLE SQUAD");
                case UnitKind.Robot: return "ROBOT";
                case UnitKind.Shieldman: return Loc.T("LÍNH KHIÊN SPARTAN", "SPARTAN");
                case UnitKind.Ghost: return Loc.T("HỒN MA", "GHOST");
                case UnitKind.Grenadier: return Loc.T("LÍNH NÉM BOM", "BOMBER");
                case UnitKind.Catapult: return Loc.T("MÁY BẮN ĐÁ", "CATAPULT");
                case UnitKind.Bat: return Loc.T("DƠI", "BAT");
                default: return Loc.T("BỘ BINH", "SOLDIER");
            }
        }

        public static string KindHint(UnitKind k)
        {
            switch (k)
            {
                case UnitKind.Cavalry: return Loc.T("Lao rất nhanh, cú đánh đầu gấp đôi. Khắc cung thủ, tháp canh", "Charges fast, first hit deals double. Beats archers and towers");
                case UnitKind.Archer: return Loc.T("Bắn xa, bắn được lính bay (x2)", "Long range, hits fliers (x2)");
                case UnitKind.Healer: return Loc.T("Hồi máu cho đồng đội ở gần", "Heals nearby allies");
                case UnitKind.Wall: return Loc.T("Đứng yên, máu cực trâu, hút đòn của địch", "Stands still, huge health, soaks up enemy hits");
                case UnitKind.Turret: return Loc.T("Đứng yên, bắn rất xa, bắn được lính bay", "Stands still, very long range, hits fliers");
                case UnitKind.Tank: return Loc.T("Đạn pháo nổ lan, rất trâu. Không bắn được lính bay", "Exploding shells, very tough. Cannot hit fliers");
                case UnitKind.Plane: return Loc.T("Ném bom mặt đất. Khắc xe tăng (x2)", "Bombs ground troops. Beats tanks (x2)");
                case UnitKind.Jet: return Loc.T("Bay cực nhanh, bắn tên lửa. Khắc lính bay (x2)", "Very fast, fires missiles. Beats fliers (x2)");
                case UnitKind.Helicopter: return Loc.T("Lơ lửng, xả súng liên tục. Khắc xe tăng", "Hovers and sprays bullets. Beats tanks");
                case UnitKind.Dragon: return Loc.T("Chúa tể bầu trời: máu trâu, sà xuống phun lửa diện rộng, trúng cả lính bay", "Lord of the sky: tough, swoops down breathing wide fire that also hits fliers");
                case UnitKind.Rifleman: return Loc.T("Vẽ đẹp ra tới 3 lính súng. Quỳ xuống bắn loạt từ xa, bắn được lính bay", "Draw well for up to 3 riflemen. Kneels and fires bursts, hits fliers");
                case UnitKind.Robot: return Loc.T("Vẽ từ 3 hình hộp trở lên. Cực trâu, đập tay xuống đất gây sát thương lan", "Built from 3+ boxes. Super tough, ground-pounds nearby enemies");
                case UnitKind.Shieldman: return Loc.T("Khiên tròn lớn + giáo: đứng hàng đầu, chặn 40% sát thương, húc khiên", "Big round shield + spear: holds the front line, blocks 40% damage");
                case UnitKind.Ghost: return Loc.T("Lơ lửng xuyên qua, lính đánh gần không chạm được. Chạm vào hút máu", "Floats past swords, melee can't touch it. Its touch drains life");
                case UnitKind.Grenadier: return Loc.T("Ném bom nổ lan qua đầu quân địch. Khắc đám đông, lính khiên", "Lobs bombs over the front line. Beats crowds and shields");
                case UnitKind.Catapult: return Loc.T("Bắn đá cực xa, nổ lan, phá thành x1.8. Chậm, yếu khi bị áp sát", "Huge range, splash, x1.8 vs castles. Slow, weak up close");
                case UnitKind.Bat: return Loc.T("Rẻ, bay nhanh, cắn cả lính bay. Vẽ đẹp ra cả đàn", "Cheap, fast flier, bites fliers too. Draw well for a swarm");
                default: return Loc.T("Đánh gần. Khắc cung thủ, thầy thuốc", "Melee. Beats archers and medics");
            }
        }

        /// <summary>One line of a kind's drawing checklist, e.g. "Cánh 1/1".</summary>
        public struct Requirement
        {
            public string label;
            public int have;
            public int need;
            public bool atMost; // "không chân": need = 0 and have must not exceed it
            public bool Done => atMost ? have <= need : have >= need;
        }
        /// <summary>Unit name for the UI; robots are named after their weapon.</summary>
        public static string DisplayName(UnitStats s)
        {
            if (s.kind != UnitKind.Robot) return KindName(s.kind);
            switch (s.robotMode)
            {
                case RobotMode.Sword: return Loc.T("ROBOT KIẾM", "SWORD ROBOT");
                case RobotMode.Cannon: return Loc.T("ROBOT ĐẠI BÁC", "CANNON ROBOT");
                case RobotMode.Flyer: return Loc.T("ROBOT BAY", "FLYING ROBOT");
                default: return Loc.T("ROBOT ĐẤM", "PUNCH ROBOT");
            }
        }

        public static string ModelName(Model m)
        {
            switch (m)
            {
                case Model.Robot: return "ROBOT";
                case Model.Dragon: return Loc.T("RỒNG", "DRAGON");
                case Model.Tank: return Loc.T("XE TĂNG", "TANK");
                case Model.Aircraft: return Loc.T("MÁY BAY", "AIRCRAFT");
                case Model.Fort: return Loc.T("CÔNG TRÌNH", "BUILDING");
                case Model.Ghost: return Loc.T("HỒN MA", "GHOST");
                default: return Loc.T("NGƯỜI", "HUMAN");
            }
        }

        /// <summary>What can be added to a model and what it turns into.</summary>
        public static string WeaponGuide(Model m)
        {
            switch (m)
            {
                case Model.Robot: return Loc.T("Không gì = đấm · kiếm chéo = Robot kiếm · nòng ngang = Robot đại bác · cánh = Robot bay", "Nothing = punch · slanted sword = Sword robot · level barrel = Cannon robot · wings = Flying robot");
                case Model.Dragon: return Loc.T("Rồng thật cần đủ: 2 cánh, 2 chân, 3 khối khép kín (đầu, thân, đuôi...), vẽ to. Thiếu = Dơi", "A real dragon needs 2 wings, 2 legs, 3 closed shapes (head, body, tail...) and to be drawn big. Less = Bat");
                case Model.Tank: return Loc.T("Nòng ngang chĩa ra trước = Xe tăng · cần ném dựng chéo lên = Máy bắn đá", "Level barrel = Tank · raised throwing arm = Catapult");
                case Model.Aircraft: return Loc.T("Cánh quạt ở mũi = Cánh quạt · không = Phản lực · nét ngang trên nóc = Trực thăng", "Propeller on the nose = Prop plane · none = Jet · line on the roof = Helicopter");
                case Model.Fort: return Loc.T("Không vũ khí = Tường · có vũ khí hoặc cung = Tháp canh", "No weapon = Wall · weapon or bow = Watchtower");
                case Model.Ghost: return Loc.T("Thân tròn có đuôi lượn sóng · thêm tay vươn ra = hút máu mạnh hơn", "Round sheet with a wavy hem · reaching arms = stronger drain");
                default: return Loc.T("Kiếm/giáo = Bộ binh · khiên tròn to = Spartan · quả bom nhỏ trên tay = Ném bom · súng = Xạ thủ · cung = Cung thủ · dấu + = Thầy thuốc · 4 chân = Kỵ binh", "Sword/spear = Soldier · big round shield = Spartan · small bomb in hand = Bomber · gun = Rifleman · bow = Archer · + sign = Medic · 4 legs = Knight");
            }
        }

        /// <summary>The weapon (or kit) the drawing was recognized with, in words.</summary>
        public static string WeaponName(UnitStats s)
        {
            switch (s.kind)
            {
                case UnitKind.Infantry: return s.weapons > 0 ? Loc.T("Kiếm/giáo", "Sword/spear") : Loc.T("Tay không", "Bare hands");
                case UnitKind.Rifleman: return Loc.T("Súng", "Rifle");
                case UnitKind.Archer: return Loc.T("Cung tên", "Bow and arrows");
                case UnitKind.Healer: return Loc.T("Dấu + (hồi máu)", "+ sign (healing)");
                case UnitKind.Cavalry: return Loc.T("Ngựa + thương", "Horse + lance");
                case UnitKind.Turret: return s.bows > 0 ? Loc.T("Nỏ lớn", "Ballista") : Loc.T("Vũ khí gắn tháp", "Tower weapon");
                case UnitKind.Wall: return Loc.T("Không", "None");
                case UnitKind.Tank: return Loc.T("Pháo", "Cannon");
                case UnitKind.Plane: return Loc.T("Bom", "Bombs");
                case UnitKind.Jet: return Loc.T("Tên lửa", "Missiles");
                case UnitKind.Helicopter: return Loc.T("Súng máy", "Machine gun");
                case UnitKind.Dragon: return Loc.T("Lửa", "Fire");
                case UnitKind.Shieldman: return s.weapons > 0 ? Loc.T("Khiên + giáo", "Shield + spear") : Loc.T("Khiên", "Shield");
                case UnitKind.Ghost: return Loc.T("Chạm hút máu", "Draining touch");
                case UnitKind.Grenadier: return Loc.T("Bom", "Bombs");
                case UnitKind.Catapult: return Loc.T("Đá tảng", "Boulders");
                case UnitKind.Bat: return Loc.T("Răng nanh", "Fangs");
                case UnitKind.Robot:
                    switch (s.robotMode)
                    {
                        case RobotMode.Sword: return Loc.T("Kiếm", "Sword");
                        case RobotMode.Cannon: return Loc.T("Đại bác", "Cannon");
                        case RobotMode.Flyer: return Loc.T("Cánh + tên lửa", "Wings + rockets");
                        default: return Loc.T("Nắm đấm", "Fists");
                    }
                default: return "";
            }
        }

        /// <summary>The body parts a model needs before its weapon decides the unit.</summary>
        public static List<Requirement> Checklist(Model m, UnitStats s)
        {
            var list = new List<Requirement>();
            void Need(string label, int have, int need) => list.Add(new Requirement { label = label, have = have, need = need });
            switch (m)
            {
                case Model.Robot: Need(Loc.T("Hộp (đầu, thân, tay)", "Boxes (head, body, arms)"), s.boxes, 2); Need(Loc.T("Chân hoặc cánh", "Legs or wings"), s.legs + s.wings, 1); break;
                case Model.Dragon:
                    Need(Loc.T("Cánh", "Wings"), s.wings, 2);
                    Need(Loc.T("Chân", "Legs"), s.legs, 2);
                    Need(Loc.T("Khối khép kín (đầu, thân, đuôi)", "Closed shapes (head, body, tail)"), s.closedShapes, 3);
                    Need(Loc.T("Vẽ to (% trang)", "Drawn big (% of page)"), Mathf.RoundToInt(s.size * 100f), DragonMinSize);
                    break;
                case Model.Tank: Need(Loc.T("Bánh xe (vòng tròn chạm đáy)", "Wheels (circles at the bottom)"), s.wheels, 2); break;
                case Model.Aircraft: Need(Loc.T("Cánh hoặc cánh quạt trên nóc", "Wings or a roof rotor"), s.wings + s.rotors, 1); break;
                case Model.Fort: Need(Loc.T("Khối khép kín", "Closed shape"), s.shields, 1); break;
                case Model.Ghost: Need(Loc.T("Thân khép kín", "Closed body"), s.shields + s.boxes, 1); list.Add(new Requirement { label = Loc.T("Không chân", "No legs"), have = s.legs, need = 0, atMost = true }); break;
                default: Need(Loc.T("Chân", "Legs"), s.legs, 1); break;
            }
            return list;
        }

        const int DragonMinSize = 55;

        /// <summary>A real dragon, not just something with wings and legs: two wings, two legs, a body made of several
        /// closed shapes (head, body, tail...) and drawn big. Anything less is a bat.</summary>
        static bool IsFullDragon(UnitStats s) =>
            s.wings >= 2 && s.legs >= 2 && s.closedShapes >= 3 && s.size * 100f >= DragonMinSize;

        public static bool Meets(Model m, UnitStats s)
        {
            foreach (var r in Checklist(m, s))
                if (!r.Done) return false;
            return true;
        }

        /// <summary>With the body settled by the chosen model, the weapon decides the exact unit.</summary>
        static UnitKind KindFor(Model m, UnitStats s)
        {
            switch (m)
            {
                case Model.Robot: return UnitKind.Robot;
                case Model.Dragon: return UnitKind.Dragon;
                case Model.Tank: return s.steepWeapons > 0 && s.steepWeapons == s.weapons ? UnitKind.Catapult : UnitKind.Tank;
                case Model.Aircraft:
                    return s.rotors > 0 ? UnitKind.Helicopter : s.propellers > 0 ? UnitKind.Plane : UnitKind.Jet;
                case Model.Fort: return s.weapons > 0 || s.bows > 0 ? UnitKind.Turret : UnitKind.Wall;
                case Model.Ghost: return UnitKind.Ghost;
                default:
                    if (s.crosses > 0) return UnitKind.Healer;
                    if (s.bows > 0) return UnitKind.Archer;
                    if (s.legs >= 4) return UnitKind.Cavalry;
                    if (s.guards > 0) return UnitKind.Shieldman;
                    if (s.bombs > 0) return UnitKind.Grenadier;
                    if (s.guns > 0) return UnitKind.Rifleman;
                    return UnitKind.Infantry;
            }
        }

        /// <summary>
        /// A ghost's sheet: a big closed outline whose bottom edge goes up and down at least three times
        /// (the wavy hem), rather than sitting flat like a wall or tower.
        /// </summary>
        static bool HasWavyHem(Doodle doodle)
        {
            Rect b = doodle.Bounds;
            float size = Mathf.Max(b.width, b.height);
            foreach (var s in doodle.strokes)
            {
                if (!IsClosed(s)) continue;
                var sb = StrokeBounds(s.points);
                if (Mathf.Max(sb.width, sb.height) < size * 0.4f) continue;
                float hemTop = sb.yMin + sb.height * 0.25f;
                float bump = sb.height * 0.04f; // ignore tiny hand wobble
                int turns = 0, dir = 0;
                float last = float.NaN;
                foreach (var p in s.points)
                {
                    if (p.y > hemTop) { last = float.NaN; dir = 0; continue; }
                    if (float.IsNaN(last)) { last = p.y; continue; }
                    float dy = p.y - last;
                    if (Mathf.Abs(dy) < bump) continue;
                    int d = dy > 0f ? 1 : -1;
                    if (dir != 0 && d != dir) turns++;
                    dir = d;
                    last = p.y;
                }
                if (turns >= 4) return true; // down-up-down-up...: at least two scallops
            }
            return false;
        }

        public static bool IsClosed(Stroke s)
        {
            int n = s.points.Count;
            if (n < 3) return false;
            float len = s.Length;
            return len > 0.12f && Vector2.Distance(s.points[0], s.points[n - 1]) < Mathf.Max(0.05f, len * 0.12f);
        }

        public static PartType[] ClassifyParts(Doodle doodle)
        {
            int count = doodle.strokes.Count;
            var parts = new PartType[count];
            Rect b = doodle.Bounds;
            float w = Mathf.Max(b.width, 0.05f);
            float h = Mathf.Max(b.height, 0.05f);
            float size = Mathf.Max(w, h);
            bool anyWing = false;

            for (int i = 0; i < count; i++)
            {
                var s = doodle.strokes[i];
                if (s.points.Count < 2) continue;

                Rect sb = StrokeBounds(s.points);
                float len = s.Length;
                Vector2 start = s.points[0], end = s.points[s.points.Count - 1];
                float straightness = len > 0.0001f ? Vector2.Distance(start, end) / len : 0f;
                bool touchesGround = sb.yMin <= b.yMin + h * 0.15f;

                if (IsClosed(s))
                {
                    bool small = Mathf.Max(sb.width, sb.height) < size * 0.35f;
                    bool tube = sb.width > sb.height * 3f && sb.xMax >= b.xMax - w * 0.15f && !touchesGround &&
                                sb.center.y > b.yMin + h * 0.35f; // barrels sit up high, not a low cart or hull
                    if (tube) parts[i] = PartType.Gun; // a long closed tube pointing forward: a gun or cannon barrel
                    else if (Rectangularity(s.points, sb) > 0.8f) parts[i] = PartType.Box; // a drawn box (robot parts, walls, hulls)
                    else parts[i] = small && touchesGround ? PartType.Wheel : PartType.Shield;
                    continue;
                }

                Vector2 delta = end - start;
                if (straightness > 0.85f)
                {
                    bool vertical = Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
                    bool flat = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 3f;

                    bool onTopMiddle = sb.center.y >= b.yMax - h * 0.06f && Mathf.Abs(sb.center.x - b.center.x) < w * 0.25f;
                    if (flat && onTopMiddle && len >= w * 0.4f)
                        parts[i] = PartType.Rotor;
                    else if (vertical && len < h * 0.8f && sb.xMin >= b.xMax - w * 0.1f)
                        parts[i] = PartType.Propeller;
                    else if (vertical && touchesGround && len > 0.04f)
                        parts[i] = PartType.Leg;
                    else if (len > 0.08f && sb.xMax >= b.xMax - w * 0.15f)
                    {
                        // Held level (within ~15°) it's a gun; slanted it's a spear or sword.
                        bool level = Mathf.Abs(delta.y) < Mathf.Abs(delta.x) * 0.27f;
                        parts[i] = level ? PartType.Gun : PartType.Weapon;
                    }
                }
                else if (straightness > 0.25f && len > 0.12f)
                {
                    if (sb.height > sb.width * 1.5f)
                        parts[i] = PartType.Bow;
                    else if (sb.width > sb.height && sb.center.y > b.yMin + h * 0.35f)
                    {
                        parts[i] = PartType.Wing;
                        anyWing = true;
                    }
                }
            }

            // A propeller only means something on a plane; otherwise it was just a front leg or a stick.
            for (int i = 0; i < count; i++)
            {
                if (parts[i] != PartType.Propeller || anyWing) continue;
                var sb = StrokeBounds(doodle.strokes[i].points);
                parts[i] = sb.yMin <= b.yMin + h * 0.15f ? PartType.Leg : PartType.None;
            }

            // A drawing made of boxes (three or more) is a robot: its tall boxes standing on the ground are its legs.
            int boxCount = 0;
            foreach (var p in parts) if (p == PartType.Box) boxCount++;
            if (boxCount >= 3)
            {
                for (int i = 0; i < count; i++)
                {
                    if (parts[i] != PartType.Box) continue;
                    var sb = StrokeBounds(doodle.strokes[i].points);
                    if (sb.yMin <= b.yMin + h * 0.15f && sb.height > sb.width * 1.2f) parts[i] = PartType.Leg;
                }
            }

            // A level stick is only a gun in the hands of a walking soldier: with a bow it's the arrow,
            // on wheels it's a cannon barrel, without legs it's a mounted weapon.
            int legCount = 0;
            bool hasBow = false, hasWheels = false;
            foreach (var p in parts)
            {
                if (p == PartType.Leg) legCount++;
                if (p == PartType.Bow) hasBow = true;
                if (p == PartType.Wheel) hasWheels = true;
            }
            if (legCount == 0 || hasBow || hasWheels || anyWing)
                for (int i = 0; i < count; i++)
                    if (parts[i] == PartType.Gun) parts[i] = PartType.Weapon;

            // A soldier (two-ish legs, no wings, wheels or robot boxes) holding a big closed shape out in front of the
            // body carries a shield: the Spartan's hoplon. The small closed shape on top is the head, not a shield.
            if (legCount > 0 && legCount < 4 && !anyWing && !hasWheels && boxCount < 3)
            {
                // "In front" is measured from the legs, so a long spear stretching the drawing doesn't move the body.
                float legsX = 0f;
                int legsN = 0;
                for (int i = 0; i < count; i++)
                    if (parts[i] == PartType.Leg) { legsX += StrokeBounds(doodle.strokes[i].points).center.x; legsN++; }
                legsX /= Mathf.Max(1, legsN);

                for (int i = 0; i < count; i++)
                {
                    if (parts[i] != PartType.Shield && parts[i] != PartType.Box) continue;
                    var sb = StrokeBounds(doodle.strokes[i].points);
                    bool inFront = sb.center.x >= legsX + w * 0.07f;
                    bool bigEnough = sb.height >= h * 0.22f;
                    bool isHead = sb.center.y > b.yMax - h * 0.3f && Mathf.Max(sb.width, sb.height) < size * 0.3f;
                    if (inFront && bigEnough && !isHead) parts[i] = PartType.Guard;
                    else if (inFront && !isHead && Mathf.Max(sb.width, sb.height) < size * 0.2f &&
                             sb.center.y > b.yMin + h * 0.3f && sb.center.y < b.yMax - h * 0.25f)
                        parts[i] = PartType.Bomb; // a little ball held out at hand height: a bomb to throw
                }

                // Emblems painted on a shield (the Spartan lambda) are decoration, not extra legs or weapons.
                for (int g = 0; g < count; g++)
                {
                    if (parts[g] != PartType.Guard) continue;
                    var gb = StrokeBounds(doodle.strokes[g].points);
                    for (int i = 0; i < count; i++)
                    {
                        if (i == g || parts[i] == PartType.Guard) continue;
                        var sb = StrokeBounds(doodle.strokes[i].points);
                        if (gb.Contains(sb.min) && gb.Contains(sb.max)) parts[i] = PartType.None;
                    }
                }
            }

            // Two short straight strokes crossing near their middles form a "+": the healer's mark.
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (IsCross(doodle.strokes[i], doodle.strokes[j], size))
                        parts[i] = parts[j] = PartType.Cross;
                }
            }
            return parts;
        }

        static bool IsCross(Stroke a, Stroke b, float size)
        {
            if (a.points.Count < 2 || b.points.Count < 2) return false;
            float la = a.Length, lb = b.Length;
            if (la > size * 0.3f || lb > size * 0.3f || la < 0.03f || lb < 0.03f) return false;
            Vector2 a0 = a.points[0], a1 = a.points[a.points.Count - 1];
            Vector2 b0 = b.points[0], b1 = b.points[b.points.Count - 1];
            if (Vector2.Distance(a0, a1) / la < 0.85f || Vector2.Distance(b0, b1) / lb < 0.85f) return false;

            Vector2 da = a1 - a0, db = b1 - b0;
            float angle = Vector2.Angle(da, db);
            if (angle < 60f || angle > 120f) return false;

            float denom = da.x * db.y - da.y * db.x;
            if (Mathf.Abs(denom) < 1e-6f) return false;
            Vector2 d = b0 - a0;
            float ta = (d.x * db.y - d.y * db.x) / denom;
            float tb = (d.x * da.y - d.y * da.x) / denom;
            return ta > 0.25f && ta < 0.75f && tb > 0.25f && tb < 0.75f;
        }

        public static UnitStats Analyze(Doodle doodle)
        {
            var stats = new UnitStats();
            Rect b = doodle.Bounds;
            float total = doodle.TotalLength;
            stats.size = Mathf.Clamp01(Mathf.Max(b.width, b.height));
            stats.inkCost = total * InkPerUnit;

            var partTypes = ClassifyParts(doodle);
            for (int i = 0; i < partTypes.Length; i++)
            {
                var p = partTypes[i];
                if (DoodleAnalyzer.IsClosed(doodle.strokes[i])) stats.closedShapes++;
                if (p == PartType.Weapon || p == PartType.Gun)
                {
                    var pts = doodle.strokes[i].points;
                    Vector2 delta = pts[pts.Count - 1] - pts[0];
                    if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * 0.7f) stats.steepWeapons++; // raised above ~35°
                }
                switch (p)
                {
                    case PartType.Leg: stats.legs++; break;
                    case PartType.Weapon: stats.weapons++; break;
                    case PartType.Shield: stats.shields++; break;
                    case PartType.Wheel: stats.wheels++; break;
                    case PartType.Wing: stats.wings++; break;
                    case PartType.Bow: stats.bows++; break;
                    case PartType.Rotor: stats.rotors++; break;
                    case PartType.Propeller: stats.propellers++; break;
                    case PartType.Cross: stats.crosses++; break;
                    case PartType.Gun: stats.guns++; stats.weapons++; break;
                    case PartType.Box: stats.boxes++; stats.shields++; break;
                    case PartType.Guard: stats.guards++; stats.shields++; break;
                    case PartType.Bomb: stats.bombs++; break;
                }
            }

            bool robotBody = stats.boxes >= 2 && stats.legs + stats.wings > 0 && stats.boxes + stats.legs + stats.wings >= 4;
            bool catapult = stats.steepWeapons > 0 && stats.steepWeapons == stats.weapons; // a raised arm, no level barrel
            if (stats.rotors > 0) stats.kind = UnitKind.Helicopter;
            else if (robotBody) stats.kind = UnitKind.Robot; // a boxy body wins over wings (a flying robot is still a robot)
            else if (stats.wings > 0)
                stats.kind = stats.legs > 0 ? (IsFullDragon(stats) ? UnitKind.Dragon : UnitKind.Bat)
                    : stats.propellers > 0 ? UnitKind.Plane : UnitKind.Jet;
            else if (stats.wheels >= 2) stats.kind = catapult ? UnitKind.Catapult : UnitKind.Tank;
            else if (stats.crosses > 0) stats.kind = UnitKind.Healer;
            else if (stats.guards > 0 && stats.legs > 0 && stats.legs < 4) stats.kind = UnitKind.Shieldman;
            else if (stats.legs == 0 && HasWavyHem(doodle)) stats.kind = UnitKind.Ghost; // a sheet with a wavy hem, no legs
            else if (stats.legs == 0)
                stats.kind = stats.bows > 0 || stats.weapons > 0 ? UnitKind.Turret : UnitKind.Wall;
            else if (stats.bows > 0) stats.kind = UnitKind.Archer;
            else if (stats.legs >= 4) stats.kind = UnitKind.Cavalry;
            else if (stats.bombs > 0) stats.kind = UnitKind.Grenadier;
            else if (stats.guns > 0) stats.kind = UnitKind.Rifleman;
            else stats.kind = UnitKind.Infantry;

            // The chosen body (model) wins once its parts are there; the weapon then picks the exact unit.
            if (doodle.intent.HasValue && Meets(doodle.intent.Value, stats)) stats.kind = KindFor(doodle.intent.Value, stats);

            // A robot fights with whatever it was given.
            stats.robotMode = stats.wings > 0 ? RobotMode.Flyer
                : stats.guns > 0 ? RobotMode.Cannon
                : stats.weapons > 0 ? RobotMode.Sword
                : RobotMode.Smash;

            int legs = Mathf.Min(stats.legs, 4);
            int weapons = Mathf.Min(stats.weapons, 3);
            int shields = Mathf.Min(stats.shields, 3);
            float sizeSpeed = Mathf.Lerp(1.5f, 0.6f, stats.size) * 1.3f;

            stats.maxHp = (10f + total * 30f) * (1f + 0.35f * shields);
            stats.damage = 3f + 4f * weapons + stats.size * 8f;
            stats.speed = (0.6f + 0.35f * legs) * sizeSpeed;
            stats.attackInterval = Mathf.Lerp(0.6f, 1.4f, stats.size);

            switch (stats.kind)
            {
                case UnitKind.Cavalry:
                    stats.maxHp *= 1.5f;
                    stats.speed *= 1.7f;
                    break;
                case UnitKind.Archer:
                    stats.maxHp *= 0.7f;
                    stats.damage *= 0.8f;
                    stats.range = 5.5f;
                    stats.attackInterval *= 1.3f;
                    break;
                case UnitKind.Robot:
                    switch (stats.robotMode)
                    {
                        case RobotMode.Sword: // big blade: heavy single hits that also clip whoever stands beside
                            stats.maxHp *= 2f;
                            stats.damage *= 1.8f;
                            stats.speed = Mathf.Min(stats.speed, 0.95f);
                            stats.splash = 0.6f;
                            stats.attackInterval = 1.15f;
                            break;
                        case RobotMode.Cannon: // arm cannon: long-range shells that burst on landing
                            stats.maxHp *= 2.6f;
                            stats.damage *= 1.6f;
                            stats.speed = Mathf.Min(stats.speed, 0.7f);
                            stats.range = 7f;
                            stats.splash = 1.3f;
                            stats.attackInterval = 2f;
                            break;
                        case RobotMode.Flyer: // jet-winged: flies, fires rockets at ground and air
                            stats.flying = true;
                            stats.maxHp *= 2.1f;
                            stats.damage *= 1.5f;
                            stats.speed = 1.7f;
                            stats.range = 5f;
                            stats.attackInterval = 1.2f;
                            break;
                        default: // fists: ground pound around the target
                            stats.maxHp *= 2.2f;
                            stats.damage *= 1.8f;
                            stats.speed = Mathf.Min(stats.speed, 0.8f);
                            stats.splash = 1.1f;
                            stats.attackInterval = 1.6f;
                            break;
                    }
                    break;
                case UnitKind.Rifleman:
                    // per soldier; one drawing becomes a squad of three
                    stats.maxHp *= 0.6f;
                    stats.damage = 4f + stats.size * 4f; // per bullet
                    stats.speed *= 0.9f;
                    stats.range = 5f;
                    stats.attackInterval = 0.14f;       // between shots of a burst
                    break;
                case UnitKind.Healer:
                    stats.maxHp *= 0.8f;
                    stats.damage = 4f + total * 3f; // heal per tick
                    stats.range = 3f;
                    stats.attackInterval = 1.2f;
                    break;
                case UnitKind.Wall:
                    stats.maxHp *= 4.5f;
                    stats.damage = 0f;
                    stats.speed = 0f;
                    break;
                case UnitKind.Turret:
                    stats.maxHp *= 1.6f;
                    stats.speed = 0f;
                    stats.range = 6.5f;
                    stats.attackInterval *= 1.2f;
                    break;
                case UnitKind.Tank:
                    stats.maxHp *= 3f;
                    stats.damage *= 1.9f;
                    stats.speed = (0.35f + 0.12f * Mathf.Min(stats.wheels, 4)) * sizeSpeed;
                    stats.range = 6.5f;
                    stats.splash = 1.2f;
                    stats.attackInterval *= 2.2f;
                    break;
                case UnitKind.Plane:
                    stats.flying = true;
                    stats.maxHp *= 0.9f;
                    stats.damage *= 1.8f;
                    stats.speed = 2.2f * Mathf.Lerp(1.2f, 0.8f, stats.size);
                    stats.splash = 1f;
                    stats.attackInterval = 1.6f;
                    break;
                case UnitKind.Jet:
                    stats.flying = true;
                    stats.maxHp *= 1f;
                    stats.damage *= 1.2f;
                    stats.speed = 5f;
                    stats.range = 4.5f;
                    stats.attackInterval = 1.4f;
                    break;
                case UnitKind.Helicopter:
                    stats.flying = true;
                    stats.damage *= 0.5f;
                    stats.speed = 1.6f;
                    stats.range = 4.5f;
                    stats.attackInterval = 0.4f;
                    break;
                case UnitKind.Shieldman:
                    // Spartan hoplite: big round shield and spear, holds the front line
                    stats.maxHp *= 1.25f;
                    stats.damage *= 0.85f;
                    stats.speed *= 0.8f;
                    stats.armor = 0.4f;
                    stats.range = stats.weapons > 0 ? 0.5f : 0f; // a spear reaches past the shield
                    stats.attackInterval *= 1.1f;
                    break;
                case UnitKind.Grenadier:
                    // lobs bombs over the front line; they burst among the crowd
                    stats.maxHp *= 0.85f;
                    stats.damage = 7f + stats.size * 6f;
                    stats.speed *= 0.9f;
                    stats.range = 4f;
                    stats.splash = 1f;
                    stats.attackInterval = 2f;
                    break;
                case UnitKind.Catapult:
                    // siege engine: huge range, slow heavy boulders, cracks castle walls
                    stats.maxHp *= 1.6f;
                    stats.damage *= 1.8f;
                    stats.speed = 0.55f;
                    stats.range = 8.5f;
                    stats.splash = 1.4f;
                    stats.attackInterval = 3.5f;
                    break;
                case UnitKind.Bat:
                    // cheap, quick little biter that flies
                    stats.flying = true;
                    stats.maxHp *= 0.55f;
                    stats.damage *= 0.8f;
                    stats.speed = 2.8f;
                    stats.attackInterval = 0.7f;
                    break;
                case UnitKind.Ghost:
                    // floats low and slips past swords; drains life on touch
                    stats.flying = true;
                    stats.maxHp *= 1.1f;
                    stats.damage = 4f + stats.size * 6f + 2f * weapons;
                    stats.speed = 1.5f;
                    stats.attackInterval = 0.9f;
                    break;
                case UnitKind.Dragon:
                    stats.flying = true;
                    stats.maxHp *= 2.4f;
                    stats.damage = (9f + stats.size * 15f) * (1f + 0.2f * weapons); // breath hits everything in front
                    stats.speed = Mathf.Max(1.3f, (0.9f + 0.2f * legs) * sizeSpeed); // big but never sluggish
                    stats.range = 2.8f;
                    stats.attackInterval = 0.85f;
                    break;
            }

            switch (doodle.color)
            {
                case InkColor.Red: // fire: hits harder, burns out faster
                    stats.damage *= 1.4f;
                    stats.maxHp *= 0.85f;
                    break;
                case InkColor.Blue: // water: tougher, slower
                    stats.maxHp *= 1.4f;
                    stats.speed *= 0.85f;
                    break;
            }

            return stats;
        }

        /// <summary>How much of its bounding box a closed shape fills: ~1 for boxes, ~0.79 for circles, ~0.5 for triangles.</summary>
        static float Rectangularity(List<Vector2> pts, Rect sb)
        {
            float area = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = pts[i], q = pts[(i + 1) % pts.Count];
                area += p.x * q.y - q.x * p.y;
            }
            float boxArea = Mathf.Max(sb.width * sb.height, 1e-6f);
            return Mathf.Abs(area) * 0.5f / boxArea;
        }

        static Rect StrokeBounds(List<Vector2> pts)
        {
            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
