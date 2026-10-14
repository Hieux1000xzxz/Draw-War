using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArtWar
{
    /// <summary>
    /// The drawing practice room (opened from the title screen): pick a unit, trace its faint guide on a free page,
    /// and see straight away what the game reads it as and how many stars it would earn. The best stars for each
    /// unit are remembered and shown in the list.
    /// </summary>
    public partial class BattleManager
    {
        PracticeView practice;
        DrawingPad practicePad;
        int practiceIndex;
        bool practiceGuide = true, practiceWasRight;
        readonly List<(Image image, TMPro.TMP_Text best)> practiceEntries = new List<(Image, TMPro.TMP_Text)>();

        static string BestKey(string label) => "artwar.practice." + label;

        void BuildPractice()
        {
            var prefab = Resources.Load<PracticeView>("UI/Practice");
            practice = prefab != null ? Instantiate(prefab) : PracticeView.Build();
            practice.name = "Practice";
            pages[Page.Practice] = practice.gameObject;

            practicePad = practice.pad.gameObject.AddComponent<DrawingPad>();
            practicePad.TryConsumeInk = _ => true; // practice is free
            practicePad.RefundInk = _ => { };
            practicePad.Locked = true;
            practicePad.Changed += RefreshPractice;

            // the unit list: picture, name and the best stars earned so far
            var samples = DoodleTemplates.Samples;
            const float h = PracticeView.EntryHeight;
            practice.listContent.sizeDelta = new Vector2(0f, samples.Length * (h + 8f) + 8f);
            for (int i = 0; i < samples.Length; i++)
            {
                int index = i;
                var (label, make) = samples[i];
                var entry = UiKit.Button(practice.listContent, "Entry " + label, Vector2.zero, new Vector2(360f, h), UiKit.Paper, 22);
                var rt = (RectTransform)entry.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(4f, -8f - i * (h + 8f));
                var name = UiKit.ButtonLabel(entry);
                name.text = DoodleAnalyzer.DisplayName(DoodleAnalyzer.Analyze(make(InkColor.Black)));
                name.color = UiKit.InkText;
                name.alignment = TMPro.TextAlignmentOptions.Left;
                name.rectTransform.offsetMin = new Vector2(96f, 30f);
                name.rectTransform.offsetMax = new Vector2(-10f, -6f);
                name.fontSharedMaterial = UiKit.Font.material;
                var picture = UiKit.Rect(entry.transform, "Picture", new Vector2(10f, 8f), new Vector2(76f, 76f)).gameObject.AddComponent<RawImage>();
                picture.texture = Thumbnail(label, make, 96, new Color(0.45f, 0.8f, 0.95f));
                picture.raycastTarget = false;
                var best = UiKit.Label(entry.transform, "Best", new Vector2(96f, 6f), new Vector2(250f, 30f), 22, TextAnchor.MiddleLeft,
                    new Color(1f, 0.7f, 0.1f), bold: true);
                OnClick(entry, () => SelectPractice(index));
                practiceEntries.Add((entry.image, best));
                RefreshBest(i);
            }

            OnClick(practice.closeButton, Back);
            OnClick(practice.undoButton, practicePad.Undo);
            OnClick(practice.clearButton, () => practicePad.Clear(false));
            OnClick(practice.prevButton, () => SelectPractice((practiceIndex - 1 + samples.Length) % samples.Length));
            OnClick(practice.nextButton, () => SelectPractice((practiceIndex + 1) % samples.Length));
            OnClick(practice.guideButton, () =>
            {
                practiceGuide = !practiceGuide;
                ShowPracticeGuide();
            });
            practice.gameObject.SetActive(false);
            SelectPractice(0);
        }

        void SelectPractice(int index)
        {
            practiceIndex = index;
            var (label, make) = DoodleTemplates.Samples[index];
            var sample = make(InkColor.Black);
            var s = DoodleAnalyzer.Analyze(sample);
            practice.targetTitle.text = Loc.T("Vẽ: ", "Draw: ") + DoodleAnalyzer.DisplayName(s);
            practice.targetPicture.texture = Thumbnail(label, make, 200, new Color(0.45f, 0.8f, 0.95f));
            DrawingTips.TryGetValue(label, out string tip);
            practice.howText.text = "✎ " + tip;
            for (int i = 0; i < practiceEntries.Count; i++)
                practiceEntries[i].image.color = i == index ? new Color(1f, 0.85f, 0.55f) : UiKit.Paper;
            practicePad.Clear(false);
            practiceWasRight = false;
            ShowPracticeGuide();
        }

        void ShowPracticeGuide()
        {
            var sample = DoodleTemplates.Samples[practiceIndex].make(InkColor.Black);
            practicePad.SetTarget(null, practiceGuide ? sample : null);
            practice.guideLabel.text = practiceGuide ? Loc.T("Hình mờ: BẬT", "Guide: ON") : Loc.T("Hình mờ: TẮT", "Guide: OFF");
        }

        /// <summary>Marks the drawing as it grows: what it reads as, right or not, and its stars.</summary>
        void RefreshPractice()
        {
            if (practice == null) return;
            var (label, make) = DoodleTemplates.Samples[practiceIndex];
            var goal = DoodleAnalyzer.Analyze(make(InkColor.Black));
            var d = practicePad.CurrentDoodle;
            var now = DoodleAnalyzer.Analyze(d);
            if (practice.checklistText != null) practice.checklistText.text = PracticeChecklist(goal, now, d);
            if (d.TotalLength < 0.05f)
            {
                practice.readText.text = Loc.T("Bắt đầu vẽ nào!", "Start drawing!");
                practice.verdictText.text = "";
                practice.starsText.text = "☆☆☆";
                practice.tipText.text = Loc.T("Nét được tô màu theo bộ phận game nhận ra: chân xanh lá, vũ khí đỏ, khiên xanh dương, cánh lơ...",
                    "Strokes are outlined by what the game sees: legs green, weapons red, shields blue, wings cyan...");
                return;
            }

            var s = now;
            bool right = s.kind == goal.kind && (s.kind != UnitKind.Robot || s.robotMode == goal.robotMode);
            practice.readText.text = Loc.T("Game đang hiểu là: ", "The game reads it as: ") + "<b>" + DoodleAnalyzer.DisplayName(s) + "</b>";
            practice.verdictText.text = right ? Loc.T("<color=#3E9B4F>✓ Đúng rồi!</color>", "<color=#3E9B4F>✓ That's it!</color>")
                : Loc.T("<color=#C8503C>✗ Chưa đúng</color>", "<color=#C8503C>✗ Not yet</color>");

            float q = right ? DoodleQuality.Score(d, s) : 0f;
            int stars = right ? DoodleQuality.Stars(q) : 0;
            practice.starsText.text = new string('★', stars) + new string('☆', 3 - stars) + (right ? $"  <size=50%>{q * 100f:0}%</size>" : "");
            practice.tipText.text = !right ? Loc.T("So với cách vẽ ở trên xem còn thiếu bộ phận nào.", "Check the how-to above for a missing part.")
                : !DoodleQuality.Multiplies(s.kind) ? Loc.T("Quân mạnh: trong trận luôn ra 1 con.", "Strong unit: always one in battle.")
                : Loc.T($"Vẽ thế này trong trận sẽ ra ×{DoodleQuality.Copies(d, s, q)} lính.", $"Drawn like this you'd get ×{DoodleQuality.Copies(d, s, q)} in battle.");

            if (right && !practiceWasRight) Sfx.Play(Sound.Twinkle, 0.5f);
            practiceWasRight = right;
            if (right && stars > PlayerPrefs.GetInt(BestKey(label), 0))
            {
                PlayerPrefs.SetInt(BestKey(label), stars);
                PlayerPrefs.Save();
                RefreshBest(practiceIndex);
            }
        }

        /// <summary>
        /// The parts the unit's model drawing has, with how many of each the player has drawn so far:
        /// "✓ Legs 2/2   ☐ Shield 0/1". Taken from the sample itself, so every unit gets its own list.
        /// </summary>
        static string PracticeChecklist(UnitStats goal, UnitStats s, Doodle d)
        {
            var items = new List<string>();
            void Need(string vi, string en, int have, int need)
            {
                if (need <= 0) return;
                bool done = have >= need;
                items.Add((done ? "<color=#3E9B4F>✓ " : "<color=#C98A1E>☐ ") + Loc.T(vi, en) + $" {Mathf.Min(have, need)}/{need}</color>");
            }
            void NoneOf(string vi, string en, int have)
            {
                items.Add((have == 0 ? "<color=#3E9B4F>✓ " : "<color=#C8503C>✗ ") + Loc.T(vi, en) + "</color>");
            }

            // only the parts that decide the unit (the same rules the game reads drawings by)
            int legs = s.legs, wings = s.wings, boxes = s.boxes, closed = s.closedShapes;
            switch (goal.kind)
            {
                case UnitKind.Infantry: Need("Chân", "Legs", legs, 2); Need("Kiếm / giáo", "Sword / spear", s.weapons - s.guns, 1); break;
                case UnitKind.Shieldman: Need("Chân", "Legs", legs, 2); Need("Khiên tròn", "Round shield", s.guards, 1); break;
                case UnitKind.Grenadier: Need("Chân", "Legs", legs, 2); Need("Quả bom", "Bomb", s.bombs, 1); break;
                case UnitKind.Rifleman: Need("Chân", "Legs", legs, 2); Need("Súng ngang", "Level gun", s.guns, 1); break;
                case UnitKind.Archer: Need("Chân", "Legs", legs, 2); Need("Cung", "Bow", s.bows, 1); break;
                case UnitKind.Healer: Need("Chân", "Legs", legs, 2); Need("Dấu +", "+ sign", s.crosses > 0 ? 1 : 0, 1); break;
                case UnitKind.Wizard: Need("Chân", "Legs", legs, 2); Need("Mũ nhọn", "Pointed hat", s.hats, 1); break;
                case UnitKind.Cavalry: Need("Chân", "Legs", legs, 4); Need("Thương", "Lance", s.weapons, 1); break;
                case UnitKind.HorseArcher: Need("Chân", "Legs", legs, 4); Need("Cung", "Bow", s.bows, 1); break;
                case UnitKind.Spider: Need("Chân", "Legs", legs, 6); break;
                case UnitKind.Wall: NoneOf("Không chân", "No legs", legs); Need("Khối khép kín", "Closed shape", closed, 1); break;
                case UnitKind.Turret:
                    NoneOf("Không chân", "No legs", legs); Need("Khối khép kín", "Closed shape", closed, 1);
                    Need("Vũ khí / cung chĩa ra", "Weapon or bow sticking out", s.outerWeapons, 1); break;
                case UnitKind.Tank: Need("Bánh xe", "Wheels", s.wheels, 2); Need("Nòng ngang", "Level barrel", s.weapons - s.steepWeapons, 1); break;
                case UnitKind.Catapult: Need("Bánh xe", "Wheels", s.wheels, 2); Need("Cần ném dựng chéo", "Raised throwing arm", s.steepWeapons, 1); break;
                case UnitKind.Plane: Need("Cánh", "Wings", wings, 1); Need("Cánh quạt ở mũi", "Nose propeller", s.propellers, 1); NoneOf("Không chân", "No legs", legs); break;
                case UnitKind.Jet: Need("Cánh", "Wings", wings, 1); NoneOf("Không chân", "No legs", legs); break;
                case UnitKind.Helicopter: Need("Cánh quạt trên nóc", "Roof rotor", s.rotors, 1); break;
                case UnitKind.Dragon:
                    Need("Cánh", "Wings", wings, 2); Need("Chân", "Legs", legs, 2);
                    Need("Khối khép kín", "Closed shapes", closed, 3); Need("Vẽ to (% trang)", "Drawn big (% of page)", Mathf.RoundToInt(s.size * 100f), 55); break;
                case UnitKind.Bat: Need("Cánh", "Wings", wings, 2); Need("Chân", "Legs", legs, 2); break;
                case UnitKind.Robot:
                    Need("Hình hộp", "Boxes", boxes, 3); Need("Chân", "Legs", legs + (goal.robotMode == RobotMode.Flyer ? wings : 0), 2);
                    if (goal.robotMode == RobotMode.Sword) Need("Kiếm chéo", "Slanted sword", s.weapons - s.guns, 1);
                    if (goal.robotMode == RobotMode.Cannon) Need("Nòng ngang", "Level barrel", s.guns, 1);
                    if (goal.robotMode == RobotMode.Flyer) Need("Cánh", "Wings", wings, 1);
                    break;
                case UnitKind.Ghost: NoneOf("Không chân", "No legs", legs); Need("Tấm khép kín đáy lượn sóng", "Closed sheet, wavy hem", closed, 1); break;
                case UnitKind.Snowman: NoneOf("Không chân", "No legs", legs); Need("Vòng tròn chồng nhau", "Stacked circles", closed, 2); break;
                case UnitKind.Balloon: NoneOf("Không chân", "No legs", legs); Need("Vòng tròn to + giỏ", "Big circle + basket", closed, 2); break;
                case UnitKind.Snake: NoneOf("Không chân", "No legs", legs); Need("Thân lượn sóng", "Wavy body", s.coils, 1); break;
            }
            return string.Join("    ", items);
        }

        void RefreshBest(int i)
        {
            int best = PlayerPrefs.GetInt(BestKey(DoodleTemplates.Samples[i].label), 0);
            practiceEntries[i].best.text = new string('★', best) + new string('☆', 3 - best);
        }
    }
}
