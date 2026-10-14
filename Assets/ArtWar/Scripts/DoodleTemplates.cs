using System;
using UnityEngine;

namespace ArtWar
{
    /// <summary>Hand-made drawings for samples, the enemy army and the castles, built from the same strokes a player would draw.</summary>
    public static class DoodleTemplates
    {
        /// <summary>One sample per unit kind, in the order shown on the sample buttons.</summary>
        public static readonly (string label, Func<InkColor, Doodle> make)[] Samples =
        {
            ("Bộ binh", StickSoldier),
            ("Lính khiên", Spartan),
            ("Ném bom", Grenadier),
            ("Xạ thủ", Rifleman),
            ("Kỵ binh", Cavalry),
            ("Cung thủ", Archer),
            ("Thầy thuốc", Healer),
            ("Tường", Wall),
            ("Tháp canh", Turret),
            ("Máy bắn đá", Catapult),
            ("Xe tăng", Tank),
            ("Cánh quạt", Plane),
            ("Phản lực", Jet),
            ("Trực thăng", Helicopter),
            ("Rồng", Dragon),
            ("Robot", Robot),
            ("Robot kiếm", RobotSword),
            ("Robot pháo", RobotCannon),
            ("Robot bay", RobotFlyer),
            ("Hồn ma", Ghost),
            ("Dơi", Bat),
            ("Nhện", Spider),
            ("Người tuyết", Snowman),
            ("Khinh khí cầu", Balloon),
        };

        /// <summary>
        /// The bodies a player can choose to draw. The guide is the bare body (shown faintly to trace over and placed by
        /// "Vẽ hộ"); whatever weapon the player adds decides the exact unit.
        /// </summary>
        public static readonly (string label, Model model, Func<InkColor, Doodle> guide)[] Models =
        {
            ("Người", Model.Human, HumanBody),
            ("Robot", Model.Robot, Robot),
            ("Rồng", Model.Dragon, Dragon),
            ("Xe tăng", Model.Tank, Tank),
            ("Máy bay", Model.Aircraft, Plane),
            ("Công trình", Model.Fort, Wall),
            ("Hồn ma", Model.Ghost, GhostBody),
        };

        /// <summary>Just the little soldier's body, ready for whatever weapon the player draws in its hands.</summary>
        public static Doodle HumanBody(InkColor color)
        {
            var d = new Doodle { color = color };
            FatBody(d, 0.45f);
            return d;
        }

        /// <summary>
        /// The little soldier of the magic-pen episode: round head, big round belly and two thin stick legs
        /// (the builder adds the feet). <paramref name="cx"/> is the belly's center line, the feet stand at y = 0.15.
        /// </summary>
        static void FatBody(Doodle d, float cx)
        {
            d.strokes.Add(Circle(new Vector2(cx + 0.02f, 0.73f), 0.065f));
            d.strokes.Add(Ellipse(new Vector2(cx, 0.51f), 0.085f, 0.12f));
            d.strokes.Add(Line(new Vector2(cx - 0.04f, 0.39f), new Vector2(cx - 0.05f, 0.15f)));
            d.strokes.Add(Line(new Vector2(cx + 0.04f, 0.39f), new Vector2(cx + 0.05f, 0.15f)));
        }

        /// <summary>Spearman: fat soldier holding a spear slanted up and forward.</summary>
        public static Doodle StickSoldier(InkColor color)
        {
            var d = new Doodle { color = color };
            FatBody(d, 0.45f);
            d.strokes.Add(Line(new Vector2(0.53f, 0.55f), new Vector2(0.6f, 0.58f)));  // arm
            d.strokes.Add(Line(new Vector2(0.38f, 0.42f), new Vector2(0.78f, 0.72f))); // spear
            return d;
        }

        /// <summary>Rifleman: fat soldier holding a rifle level at the shoulder.</summary>
        public static Doodle Rifleman(InkColor color)
        {
            var d = new Doodle { color = color };
            FatBody(d, 0.45f);
            d.strokes.Add(Line(new Vector2(0.4f, 0.57f), new Vector2(0.8f, 0.58f)));    // rifle
            d.strokes.Add(Line(new Vector2(0.4f, 0.57f), new Vector2(0.37f, 0.52f)));   // stock
            d.strokes.Add(Line(new Vector2(0.53f, 0.53f), new Vector2(0.58f, 0.565f))); // arm
            return d;
        }

        /// <summary>Knight on a fat horse: four thin legs, little horse head, small rider with a raised lance.</summary>
        public static Doodle Cavalry(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Ellipse(new Vector2(0.45f, 0.42f), 0.19f, 0.085f));               // horse belly
            foreach (float x in new[] { 0.31f, 0.38f, 0.52f, 0.59f })
                d.strokes.Add(Line(new Vector2(x, 0.35f), new Vector2(x + 0.01f, 0.12f)));    // legs
            d.strokes.Add(Line(new Vector2(0.62f, 0.47f), new Vector2(0.72f, 0.58f)));        // neck
            d.strokes.Add(Ellipse(new Vector2(0.76f, 0.6f), 0.055f, 0.035f));                 // horse head
            d.strokes.Add(Line(new Vector2(0.26f, 0.45f), new Vector2(0.17f, 0.35f)));        // tail
            d.strokes.Add(Ellipse(new Vector2(0.45f, 0.6f), 0.06f, 0.08f));                   // rider
            d.strokes.Add(Circle(new Vector2(0.46f, 0.74f), 0.045f));                         // rider head
            d.strokes.Add(Line(new Vector2(0.4f, 0.58f), new Vector2(0.92f, 0.78f)));         // lance
            return d;
        }

        /// <summary>
        /// Spartan hoplite: crested helmet, fat belly behind a big round shield (hoplon) with a lambda on it,
        /// and a long spear slanted up past the shield.
        /// </summary>
        public static Doodle Spartan(InkColor color)
        {
            var d = new Doodle { color = color };
            const float cx = 0.42f;
            // crested helmet, one closed outline: the round helmet under a tall arched horsehair crest running
            // front to back (the Spartan side view)
            var helmet = new Stroke();
            for (int i = 0; i <= 18; i++) // helmet: from the front top, down round the face and chin, up the back
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(40f, -220f, i / 18f);
                helmet.points.Add(new Vector2(0.44f + Mathf.Cos(a) * 0.068f, 0.73f + Mathf.Sin(a) * 0.068f));
            }
            for (int i = 0; i <= 20; i++) // crest: from the back of the head, arching high over to the brow, tufted
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(172f, 18f, i / 20f);
                float tuft = i % 2 == 0 ? 1f : 0.86f; // horsehair bristles along the top
                helmet.points.Add(new Vector2(0.425f + Mathf.Cos(a) * 0.115f * tuft, 0.77f + Mathf.Sin(a) * 0.14f * tuft));
            }
            helmet.points.Add(helmet.points[0]);
            d.strokes.Add(helmet);
            d.strokes.Add(Ellipse(new Vector2(cx, 0.51f), 0.085f, 0.12f));                         // belly
            d.strokes.Add(Line(new Vector2(cx - 0.04f, 0.39f), new Vector2(cx - 0.05f, 0.15f)));   // legs
            d.strokes.Add(Line(new Vector2(cx + 0.04f, 0.39f), new Vector2(cx + 0.05f, 0.15f)));
            d.strokes.Add(Line(new Vector2(0.30f, 0.55f), new Vector2(0.88f, 0.77f)));             // spear, overhand above the shield
            d.strokes.Add(Circle(new Vector2(0.53f, 0.49f), 0.13f));                               // hoplon
            d.strokes.Add(Polyline(false, new Vector2(0.48f, 0.43f), new Vector2(0.53f, 0.56f),
                new Vector2(0.58f, 0.43f)));                                                          // Λ
            return d;
        }

        /// <summary>Bomb thrower: fat soldier holding a round bomb with a short fuse out in front.</summary>
        public static Doodle Grenadier(InkColor color)
        {
            var d = new Doodle { color = color };
            FatBody(d, 0.45f);
            d.strokes.Add(Line(new Vector2(0.53f, 0.55f), new Vector2(0.58f, 0.56f)));            // arm
            d.strokes.Add(Circle(new Vector2(0.615f, 0.56f), 0.04f));                            // bomb
            d.strokes.Add(Polyline(false, new Vector2(0.635f, 0.598f), new Vector2(0.65f, 0.625f),
                new Vector2(0.67f, 0.63f)));                                                        // fuse
            return d;
        }

        /// <summary>Catapult: a wooden cart on two wheels, an A-frame, and a throwing arm raised high with its bucket.</summary>
        public static Doodle Catapult(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Box(0.22f, 0.2f, 0.78f, 0.3f));                                           // cart
            d.strokes.Add(Circle(new Vector2(0.33f, 0.14f), 0.065f));                               // wheels
            d.strokes.Add(Circle(new Vector2(0.67f, 0.14f), 0.065f));
            d.strokes.Add(Polyline(false, new Vector2(0.42f, 0.3f), new Vector2(0.5f, 0.47f),
                new Vector2(0.58f, 0.3f)));                                                         // A-frame
            d.strokes.Add(Line(new Vector2(0.36f, 0.36f), new Vector2(0.73f, 0.77f)));            // throwing arm
            d.strokes.Add(Circle(new Vector2(0.76f, 0.8f), 0.05f));                                 // bucket
            d.strokes.Add(Box(0.27f, 0.31f, 0.36f, 0.4f));                                          // counterweight
            return d;
        }

        /// <summary>
        /// Bat: little round body, a head with two pointy ears in one outline, two scalloped wings spread wide and
        /// two tiny feet. Wings and legs without a dragon's big many-part body make a bat.
        /// </summary>
        public static Doodle Bat(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Ellipse(new Vector2(0.5f, 0.5f), 0.07f, 0.09f));                          // body
            d.strokes.Add(Polyline(true,
                new Vector2(0.45f, 0.6f), new Vector2(0.44f, 0.66f), new Vector2(0.45f, 0.72f), new Vector2(0.475f, 0.685f),
                new Vector2(0.525f, 0.685f), new Vector2(0.55f, 0.72f), new Vector2(0.56f, 0.66f), new Vector2(0.55f, 0.6f),
                new Vector2(0.5f, 0.585f)));                                                        // head with ears
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 P(float x, float y) => new Vector2(0.5f + side * x, y);
                d.strokes.Add(Polyline(false, P(0.06f, 0.56f), P(0.14f, 0.66f), P(0.24f, 0.68f), P(0.33f, 0.6f),
                    P(0.28f, 0.56f), P(0.23f, 0.52f)));                                                // scalloped wing
            }
            d.strokes.Add(Line(new Vector2(0.48f, 0.415f), new Vector2(0.47f, 0.34f)));            // feet
            d.strokes.Add(Line(new Vector2(0.52f, 0.415f), new Vector2(0.53f, 0.34f)));
            return d;
        }

        /// <summary>Spider: a round body and a little head, with eight straight legs splayed out and down to the ground.</summary>
        public static Doodle Spider(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Ellipse(new Vector2(0.5f, 0.42f), 0.14f, 0.1f));            // body
            d.strokes.Add(Circle(new Vector2(0.67f, 0.45f), 0.065f));                  // head
            float[] hips = { 0.41f, 0.45f, 0.49f, 0.53f }, feet = { 0.12f, 0.22f, 0.32f, 0.42f };
            for (int i = 0; i < 4; i++)
            {
                d.strokes.Add(Line(new Vector2(hips[i], 0.36f), new Vector2(feet[i], 0.1f)));          // left legs
                d.strokes.Add(Line(new Vector2(1f - hips[i] + 0.06f, 0.36f), new Vector2(1f - feet[i] + 0.06f, 0.1f))); // right legs
            }
            return d;
        }

        /// <summary>Snowman: a big snowball, a smaller one on top and a head, twig arms out to the sides.</summary>
        public static Doodle Snowman(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Circle(new Vector2(0.5f, 0.27f), 0.17f));                   // bottom ball
            d.strokes.Add(Circle(new Vector2(0.5f, 0.55f), 0.12f));                   // middle ball
            d.strokes.Add(Circle(new Vector2(0.5f, 0.75f), 0.085f));                  // head
            d.strokes.Add(Polyline(false, new Vector2(0.39f, 0.58f), new Vector2(0.27f, 0.66f), new Vector2(0.22f, 0.72f))); // twig arms
            d.strokes.Add(Polyline(false, new Vector2(0.61f, 0.58f), new Vector2(0.73f, 0.66f), new Vector2(0.78f, 0.72f)));
            return d;
        }

        /// <summary>Hot-air balloon: a big round envelope with a band, two ropes down to a little basket.</summary>
        public static Doodle Balloon(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Ellipse(new Vector2(0.5f, 0.64f), 0.2f, 0.23f));            // envelope
            d.strokes.Add(Line(new Vector2(0.37f, 0.47f), new Vector2(0.45f, 0.27f))); // ropes
            d.strokes.Add(Line(new Vector2(0.63f, 0.47f), new Vector2(0.55f, 0.27f)));
            d.strokes.Add(Box(0.43f, 0.13f, 0.57f, 0.27f));                           // basket
            return d;
        }

        /// <summary>
        /// Ghost: a round-topped sheet with a wavy hem and two arms reaching forward. Tagged as a ghost so it is
        /// not mistaken for a wall when it arrives without a chosen body (enemy samples).
        /// </summary>
        public static Doodle Ghost(InkColor color)
        {
            var d = new Doodle { color = color, intent = Model.Ghost };
            var sheet = new Stroke();
            sheet.points.Add(new Vector2(0.37f, 0.36f));
            for (int i = 0; i <= 16; i++) // left side up, over the dome, down the right side
            {
                float a = Mathf.PI - Mathf.PI * i / 16f;
                sheet.points.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.13f, 0.6f + Mathf.Sin(a) * 0.17f));
            }
            sheet.points.Add(new Vector2(0.63f, 0.36f));
            for (int i = 1; i <= 8; i++) // wavy hem back to the start
            {
                float x = 0.63f - 0.26f * i / 8f;
                sheet.points.Add(new Vector2(x, i % 2 == 1 ? 0.29f : 0.35f));
            }
            d.strokes.Add(sheet);
            d.strokes.Add(Polyline(false, new Vector2(0.62f, 0.55f), new Vector2(0.71f, 0.56f), new Vector2(0.77f, 0.52f)));
            d.strokes.Add(Polyline(false, new Vector2(0.62f, 0.47f), new Vector2(0.70f, 0.47f), new Vector2(0.75f, 0.43f)));
            return d;
        }

        /// <summary>The ghost's bare sheet, for the guide.</summary>
        public static Doodle GhostBody(InkColor color)
        {
            var d = Ghost(color);
            d.strokes.RemoveRange(1, d.strokes.Count - 1);
            return d;
        }

        /// <summary>
        /// The neat drawing of each unit kind, which a player's drawing is compared with to judge how well it is drawn.
        /// </summary>
        public static Doodle Reference(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Rifleman: return Rifleman(InkColor.Black);
                case UnitKind.Archer: return Archer(InkColor.Black);
                case UnitKind.Healer: return Healer(InkColor.Black);
                case UnitKind.Shieldman: return Spartan(InkColor.Black);
                case UnitKind.Cavalry: return Cavalry(InkColor.Black);
                case UnitKind.Ghost: return Ghost(InkColor.Black);
                case UnitKind.Grenadier: return Grenadier(InkColor.Black);
                case UnitKind.Bat: return Bat(InkColor.Black);
                case UnitKind.Spider: return Spider(InkColor.Black);
                default: return StickSoldier(InkColor.Black);
            }
        }

        public static Doodle Archer(InkColor color)
        {
            var d = new Doodle { color = color };
            FatBody(d, 0.42f);
            d.strokes.Add(Arc(new Vector2(0.58f, 0.55f), 0.16f, 0.17f, -70f, 70f));           // bow
            d.strokes.Add(Line(new Vector2(0.44f, 0.55f), new Vector2(0.8f, 0.55f)));          // arrow
            return d;
        }

        public static Doodle Healer(InkColor color)
        {
            var d = new Doodle { color = color };
            FatBody(d, 0.45f);
            d.strokes.Add(Line(new Vector2(0.41f, 0.52f), new Vector2(0.49f, 0.52f)));         // + on the belly
            d.strokes.Add(Line(new Vector2(0.45f, 0.48f), new Vector2(0.45f, 0.56f)));
            return d;
        }

        public static Doodle Wall(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Polyline(true,
                new Vector2(0.2f, 0.05f), new Vector2(0.8f, 0.05f), new Vector2(0.8f, 0.6f), new Vector2(0.2f, 0.6f)));
            d.strokes.Add(Line(new Vector2(0.28f, 0.24f), new Vector2(0.6f, 0.24f)));
            d.strokes.Add(Line(new Vector2(0.4f, 0.42f), new Vector2(0.7f, 0.42f)));
            return d;
        }

        public static Doodle Turret(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Polyline(true,
                new Vector2(0.3f, 0.05f), new Vector2(0.6f, 0.05f), new Vector2(0.6f, 0.5f), new Vector2(0.3f, 0.5f)));
            d.strokes.Add(Arc(new Vector2(0.58f, 0.64f), 0.12f, 0.12f, -70f, 70f));
            d.strokes.Add(Line(new Vector2(0.48f, 0.64f), new Vector2(0.85f, 0.64f)));
            return d;
        }

        public static Doodle Tank(InkColor color)
        {
            // The cartoon tank: a pill-shaped track with road wheels inside, a box hull, a turret and a long barrel.
            var d = new Doodle { color = color };
            d.strokes.Add(Capsule(0.14f, 0.8f, 0.15f, 0.095f));                                  // track
            foreach (float x in new[] { 0.25f, 0.39f, 0.53f, 0.67f })
                d.strokes.Add(Circle(new Vector2(x, 0.15f), 0.052f));                             // road wheels
            d.strokes.Add(Polyline(true,
                new Vector2(0.18f, 0.245f), new Vector2(0.78f, 0.245f), new Vector2(0.74f, 0.4f), new Vector2(0.22f, 0.4f))); // hull
            d.strokes.Add(Polyline(true,
                new Vector2(0.34f, 0.4f), new Vector2(0.62f, 0.4f), new Vector2(0.58f, 0.53f), new Vector2(0.38f, 0.53f))); // turret
            d.strokes.Add(Line(new Vector2(0.6f, 0.47f), new Vector2(0.97f, 0.47f)));             // barrel
            return d;
        }

        /// <summary>Cartoon prop plane: cigar fuselage, round cockpit window, swept wing, tail fin and a prop on the nose.</summary>
        public static Doodle Plane(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Ellipse(new Vector2(0.48f, 0.45f), 0.26f, 0.08f));                    // fuselage
            d.strokes.Add(Circle(new Vector2(0.6f, 0.52f), 0.04f));                              // cockpit window
            d.strokes.Add(Polyline(false,
                new Vector2(0.3f, 0.47f), new Vector2(0.48f, 0.58f), new Vector2(0.6f, 0.46f))); // wing
            d.strokes.Add(Polyline(false,
                new Vector2(0.24f, 0.5f), new Vector2(0.2f, 0.63f), new Vector2(0.3f, 0.52f)));   // tail fin
            d.strokes.Add(Line(new Vector2(0.75f, 0.37f), new Vector2(0.75f, 0.53f)));           // propeller
            return d;
        }

        /// <summary>Cartoon jet: pointed body, bubble canopy, swept-back wing and tail.</summary>
        public static Doodle Jet(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Polyline(true,
                new Vector2(0.14f, 0.42f), new Vector2(0.68f, 0.42f), new Vector2(0.92f, 0.47f),
                new Vector2(0.68f, 0.52f), new Vector2(0.14f, 0.52f)));                          // body
            d.strokes.Add(Circle(new Vector2(0.66f, 0.52f), 0.04f));                             // canopy
            d.strokes.Add(Polyline(false,
                new Vector2(0.28f, 0.48f), new Vector2(0.46f, 0.59f), new Vector2(0.58f, 0.47f))); // wing
            d.strokes.Add(Polyline(false,
                new Vector2(0.12f, 0.52f), new Vector2(0.15f, 0.66f), new Vector2(0.26f, 0.52f))); // tail
            d.strokes.Add(Polyline(false,
                new Vector2(0.13f, 0.44f), new Vector2(0.05f, 0.47f), new Vector2(0.13f, 0.5f)));  // exhaust flame
            return d;
        }

        /// <summary>Cartoon helicopter: bubble body with a round window, tail boom, rotor on a mast and landing skids.</summary>
        public static Doodle Helicopter(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Ellipse(new Vector2(0.47f, 0.41f), 0.15f, 0.1f));                      // body
            d.strokes.Add(Circle(new Vector2(0.55f, 0.44f), 0.045f));                            // window
            d.strokes.Add(Line(new Vector2(0.32f, 0.43f), new Vector2(0.1f, 0.47f)));            // tail boom
            d.strokes.Add(Line(new Vector2(0.1f, 0.42f), new Vector2(0.1f, 0.52f)));             // tail rotor
            d.strokes.Add(Line(new Vector2(0.47f, 0.51f), new Vector2(0.47f, 0.6f)));            // mast
            d.strokes.Add(Line(new Vector2(0.2f, 0.6f), new Vector2(0.74f, 0.6f)));              // rotor
            d.strokes.Add(Line(new Vector2(0.4f, 0.31f), new Vector2(0.4f, 0.22f)));             // skid strut
            d.strokes.Add(Line(new Vector2(0.32f, 0.22f), new Vector2(0.6f, 0.22f)));            // skid
            return d;
        }

        /// <summary>
        /// A fierce wyvern like the magic pen's pencil dragon: two great bat wings spread high, each a smooth leading-edge
        /// bone with membrane panels between the fingers and scalloped trailing edges; a muscular torso, long neck,
        /// horned head with open fanged jaws, clawed legs and a long tapering tail curling back.
        /// Drawings with wings and legs are rendered in pencil (sketchy multi-pass outlines over a pale wash).
        /// </summary>
        public static Doodle Dragon(InkColor color)
        {
            var d = new Doodle { color = color };

            // far wing (behind the head, up and forward)
            Vector2 fw = new Vector2(0.7f, 0.86f);
            d.strokes.Add(Smooth(Polyline(false,
                new Vector2(0.56f, 0.5f), new Vector2(0.6f, 0.7f), fw, new Vector2(0.86f, 0.94f),
                new Vector2(1f, 0.9f), new Vector2(1.08f, 0.8f)), false));
            d.strokes.Add(Panel(fw, new[] { new Vector2(0.86f, 0.94f), new Vector2(1f, 0.9f), new Vector2(1.08f, 0.8f) }, new Vector2(1f, 0.68f)));
            d.strokes.Add(Panel(fw, new[] { new Vector2(1f, 0.68f) }, new Vector2(0.86f, 0.6f)));
            d.strokes.Add(Panel(fw, new[] { new Vector2(0.86f, 0.6f) }, new Vector2(0.64f, 0.6f), new Vector2(0.6f, 0.7f)));

            // near wing: a huge sweep from the shoulder up to the wrist and out to the tip
            Vector2 nw = new Vector2(0.32f, 0.92f);
            d.strokes.Add(Smooth(Polyline(false,
                new Vector2(0.46f, 0.46f), new Vector2(0.44f, 0.72f), nw, new Vector2(0.12f, 0.99f),
                new Vector2(-0.04f, 0.95f), new Vector2(-0.14f, 0.86f)), false));
            d.strokes.Add(Panel(nw, new[] { new Vector2(0.12f, 0.99f), new Vector2(-0.04f, 0.95f), new Vector2(-0.14f, 0.86f) }, new Vector2(-0.08f, 0.68f)));
            d.strokes.Add(Panel(nw, new[] { new Vector2(-0.08f, 0.68f) }, new Vector2(0.08f, 0.59f)));
            d.strokes.Add(Panel(nw, new[] { new Vector2(0.08f, 0.59f) }, new Vector2(0.42f, 0.56f), new Vector2(0.44f, 0.72f)));

            // muscular torso and neck (smooth curves), head with open jaws, fangs and horns
            d.strokes.Add(Smooth(Polyline(true,
                new Vector2(0.64f, 0.49f), new Vector2(0.67f, 0.42f), new Vector2(0.6f, 0.35f), new Vector2(0.48f, 0.32f),
                new Vector2(0.37f, 0.34f), new Vector2(0.31f, 0.41f), new Vector2(0.37f, 0.48f), new Vector2(0.5f, 0.51f)), true));
            d.strokes.Add(Smooth(Polyline(true,
                new Vector2(0.58f, 0.47f), new Vector2(0.66f, 0.58f), new Vector2(0.74f, 0.63f), new Vector2(0.78f, 0.58f),
                new Vector2(0.7f, 0.52f), new Vector2(0.65f, 0.42f)), true));
            d.strokes.Add(Polyline(true,
                new Vector2(0.74f, 0.6f), new Vector2(0.8f, 0.67f), new Vector2(0.9f, 0.66f), new Vector2(0.97f, 0.62f),
                new Vector2(0.88f, 0.605f), new Vector2(0.95f, 0.56f), new Vector2(0.82f, 0.55f), new Vector2(0.77f, 0.56f)));
            d.strokes.Add(Polyline(false,
                new Vector2(0.86f, 0.603f), new Vector2(0.872f, 0.585f), new Vector2(0.884f, 0.602f), new Vector2(0.896f, 0.585f))); // fangs
            d.strokes.Add(Line(new Vector2(0.8f, 0.66f), new Vector2(0.74f, 0.76f)));             // horns
            d.strokes.Add(Line(new Vector2(0.84f, 0.665f), new Vector2(0.8f, 0.77f)));

            // legs
            d.strokes.Add(Line(new Vector2(0.42f, 0.34f), new Vector2(0.4f, 0.14f)));
            d.strokes.Add(Line(new Vector2(0.56f, 0.35f), new Vector2(0.58f, 0.16f)));

            // long tail tapering and curling back, with a spade tip
            d.strokes.Add(Smooth(Taper(new[]
            {
                new Vector2(0.35f, 0.4f), new Vector2(0.24f, 0.35f), new Vector2(0.14f, 0.31f), new Vector2(0.06f, 0.33f),
                new Vector2(0.04f, 0.4f), new Vector2(0.08f, 0.45f),
            }, 0.035f, 0.006f), true));
            d.strokes.Add(Polyline(true, new Vector2(0.08f, 0.45f), new Vector2(0.06f, 0.5f), new Vector2(0.12f, 0.48f)));
            return d;
        }

        /// <summary>
        /// One membrane panel of a bat wing: from the wrist along the given edge points, then a scalloped (concave)
        /// trailing edge to <paramref name="nextTip"/>, then back to the wrist along the next finger (or via <paramref name="via"/>).
        /// </summary>
        static Stroke Panel(Vector2 wrist, Vector2[] edge, Vector2 nextTip, Vector2? via = null)
        {
            var pts = new System.Collections.Generic.List<Vector2> { wrist };
            pts.AddRange(edge);
            Vector2 from = edge[edge.Length - 1];
            Vector2 control = Vector2.Lerp((from + nextTip) * 0.5f, wrist, 0.3f); // scallop bulges in toward the wrist
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                pts.Add((1 - t) * (1 - t) * from + 2 * (1 - t) * t * control + t * t * nextTip);
            }
            if (via.HasValue) pts.Add(via.Value);
            return Polyline(true, pts.ToArray());
        }

        /// <summary>Rounds off a polyline (Chaikin corner cutting) so it reads as an organic curve.</summary>
        static Stroke Smooth(Stroke s, bool closed, int iterations = 2)
        {
            var cur = s.points;
            for (int it = 0; it < iterations; it++)
            {
                var next = new System.Collections.Generic.List<Vector2>();
                if (!closed) next.Add(cur[0]);
                for (int i = 0; i < cur.Count - 1; i++)
                {
                    next.Add(Vector2.Lerp(cur[i], cur[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(cur[i], cur[i + 1], 0.75f));
                }
                if (closed) next.Add(next[0]);
                else next.Add(cur[cur.Count - 1]);
                cur = next;
            }
            var result = new Stroke();
            result.points.AddRange(cur);
            return result;
        }

        /// <summary>A closed outline around a centerline, thick at the start and thin at the end (tails, necks).</summary>
        internal static Stroke Taper(Vector2[] centerline, float w0, float w1)
        {
            var left = new System.Collections.Generic.List<Vector2>();
            var right = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < centerline.Length; i++)
            {
                Vector2 dir = centerline[Mathf.Min(i + 1, centerline.Length - 1)] - centerline[Mathf.Max(i - 1, 0)];
                Vector2 n = new Vector2(-dir.y, dir.x).normalized;
                float w = Mathf.Lerp(w0, w1, (float)i / (centerline.Length - 1));
                left.Add(centerline[i] + n * w);
                right.Add(centerline[i] - n * w);
            }
            right.Reverse();
            left.AddRange(right);
            return Polyline(true, left.ToArray());
        }

        /// <summary>The boxy robot: box head with an antenna, box body with buttons, box arms and box legs.</summary>
        public static Doodle Robot(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Box(0.42f, 0.68f, 0.58f, 0.8f));                                        // head
            d.strokes.Add(Line(new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.88f)));              // antenna
            d.strokes.Add(Line(new Vector2(0.45f, 0.71f), new Vector2(0.55f, 0.71f)));           // mouth grill
            d.strokes.Add(Box(0.34f, 0.38f, 0.66f, 0.66f));                                       // body
            foreach (float y in new[] { 0.58f, 0.5f })
                d.strokes.Add(Circle(new Vector2(0.5f, y), 0.025f));                              // buttons
            d.strokes.Add(Box(0.24f, 0.42f, 0.32f, 0.64f));                                       // arms
            d.strokes.Add(Box(0.68f, 0.42f, 0.76f, 0.64f));
            d.strokes.Add(Box(0.38f, 0.06f, 0.47f, 0.36f));                                       // legs
            d.strokes.Add(Box(0.53f, 0.06f, 0.62f, 0.36f));
            return d;
        }

        /// <summary>
        /// Robot holding a broadsword up and forward in its right hand: the sword's spine line (what the game reads
        /// as the weapon), a pointed blade around it, a crossguard and a round pommel.
        /// </summary>
        public static Doodle RobotSword(InkColor color)
        {
            var d = Robot(color);
            Vector2 grip = new Vector2(0.74f, 0.44f), guard = new Vector2(0.78f, 0.5f), point = new Vector2(0.99f, 0.82f);
            d.strokes.Add(Line(grip, point));                                                         // spine
            d.strokes.Add(Taper(new[] { guard, Vector2.Lerp(guard, point, 0.5f), point }, 0.04f, 0.004f));  // broad blade
            d.strokes.Add(Line(new Vector2(0.735f, 0.535f), new Vector2(0.825f, 0.465f)));           // crossguard
            d.strokes.Add(Circle(new Vector2(0.73f, 0.425f), 0.022f));                              // pommel
            return d;
        }

        /// <summary>
        /// Robot with an arm cannon: a long barrel tube held in the right hand, a round breech at the back,
        /// a reinforcing band and a thick muzzle ring.
        /// </summary>
        public static Doodle RobotCannon(InkColor color)
        {
            var d = Robot(color);
            d.strokes.Add(Box(0.72f, 0.475f, 1.02f, 0.565f));                                       // barrel
            d.strokes.Add(Circle(new Vector2(0.72f, 0.52f), 0.055f));                               // breech
            d.strokes.Add(Line(new Vector2(0.86f, 0.475f), new Vector2(0.86f, 0.565f)));           // band
            d.strokes.Add(Circle(new Vector2(1.03f, 0.52f), 0.05f));                                // muzzle ring
            return d;
        }

        /// <summary>Robot with jet wings on its back.</summary>
        public static Doodle RobotFlyer(InkColor color)
        {
            var d = Robot(color);
            d.strokes.Add(Polyline(false,
                new Vector2(0.36f, 0.62f), new Vector2(0.24f, 0.8f), new Vector2(0.1f, 0.82f), new Vector2(0.02f, 0.72f)));
            d.strokes.Add(Polyline(false,
                new Vector2(0.64f, 0.62f), new Vector2(0.76f, 0.8f), new Vector2(0.9f, 0.82f), new Vector2(0.98f, 0.72f)));
            return d;
        }

        static Stroke Box(float x0, float y0, float x1, float y1) =>
            Polyline(true, new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1));

        public static Doodle Bug(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Ellipse(new Vector2(0.5f, 0.34f), 0.2f, 0.09f));
            foreach (float x in new[] { 0.36f, 0.46f, 0.56f, 0.64f })
                d.strokes.Add(Line(new Vector2(x, 0.26f), new Vector2(x - 0.03f, 0.15f)));
            d.strokes.Add(Line(new Vector2(0.69f, 0.36f), new Vector2(0.8f, 0.38f)));
            return d;
        }

        public static Doodle Giant(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Circle(new Vector2(0.5f, 0.52f), 0.34f));
            d.strokes.Add(Circle(new Vector2(0.6f, 0.62f), 0.04f));
            d.strokes.Add(Line(new Vector2(0.84f, 0.58f), new Vector2(0.98f, 0.6f)));
            d.strokes.Add(Line(new Vector2(0.82f, 0.42f), new Vector2(0.97f, 0.4f)));
            d.strokes.Add(Line(new Vector2(0.4f, 0.21f), new Vector2(0.38f, 0.04f)));
            d.strokes.Add(Line(new Vector2(0.6f, 0.21f), new Vector2(0.62f, 0.04f)));
            return d;
        }

        public static Doodle ArrowShot()
        {
            var d = new Doodle();
            d.strokes.Add(Line(new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.5f)));
            d.strokes.Add(Polyline(false, new Vector2(0.7f, 0.58f), new Vector2(0.8f, 0.5f), new Vector2(0.7f, 0.42f)));
            return d;
        }

        public static Doodle MissileShot()
        {
            var d = new Doodle { color = InkColor.Red };
            d.strokes.Add(Polyline(true,
                new Vector2(0.2f, 0.45f), new Vector2(0.7f, 0.45f), new Vector2(0.82f, 0.5f),
                new Vector2(0.7f, 0.55f), new Vector2(0.2f, 0.55f)));
            return d;
        }

        public static Doodle BallShot(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Circle(new Vector2(0.5f, 0.5f), 0.12f));
            return d;
        }

        public static Doodle BulletShot()
        {
            var d = new Doodle();
            d.strokes.Add(Line(new Vector2(0.4f, 0.5f), new Vector2(0.6f, 0.5f)));
            return d;
        }

        public static Doodle HealPuff()
        {
            var d = new Doodle { color = InkColor.Blue };
            d.strokes.Add(Line(new Vector2(0.35f, 0.5f), new Vector2(0.65f, 0.5f)));
            d.strokes.Add(Line(new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.65f)));
            return d;
        }

        /// <summary>A filled star burst at the muzzle (black ink on a neutral drawing = yellow crayon).</summary>
        public static Doodle MuzzleFlash()
        {
            var d = new Doodle();
            var star = new Vector2[10];
            for (int i = 0; i < star.Length; i++)
            {
                float a = i * Mathf.PI * 2f / star.Length;
                float r = i % 2 == 0 ? 0.3f : 0.12f;
                star[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r);
            }
            d.strokes.Add(Polyline(true, star));
            return d;
        }

        public static Doodle Sparkle()
        {
            var d = new Doodle { color = InkColor.Blue };
            d.strokes.Add(Line(new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.5f)));
            d.strokes.Add(Line(new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.7f)));
            d.strokes.Add(Line(new Vector2(0.37f, 0.37f), new Vector2(0.63f, 0.63f)));
            d.strokes.Add(Line(new Vector2(0.37f, 0.63f), new Vector2(0.63f, 0.37f)));
            return d;
        }

        public static Doodle FirePuff()
        {
            // a closed flame tongue so it gets a bright crayon fill
            var d = new Doodle { color = InkColor.Red };
            d.strokes.Add(Polyline(true,
                new Vector2(0.38f, 0.38f), new Vector2(0.42f, 0.55f), new Vector2(0.48f, 0.48f), new Vector2(0.52f, 0.68f),
                new Vector2(0.57f, 0.5f), new Vector2(0.62f, 0.58f), new Vector2(0.64f, 0.38f), new Vector2(0.5f, 0.32f)));
            return d;
        }

        /// <summary>
        /// A fortress, drawn in canvas space with its base at (0.5, 0.05): two squat corner towers, a curtain wall,
        /// a tall central keep, battlements everywhere, arrow slits, a portcullis gate and a few stone courses.
        /// The banner is drawn separately (<see cref="CastleBanner"/>) so it can wear the team's color.
        /// </summary>
        public static Doodle Castle(InkColor color)
        {
            var d = new Doodle { color = color };

            // corner towers, wider at the base like a real keep
            foreach (float x0 in new[] { 0.04f, 0.74f })
            {
                float x1 = x0 + 0.22f;
                d.strokes.Add(Polyline(true, new Vector2(x0 - 0.02f, 0.05f), new Vector2(x1 + 0.02f, 0.05f),
                    new Vector2(x1, 0.66f), new Vector2(x0, 0.66f)));
                d.strokes.Add(Battlements(x0 - 0.01f, x1 + 0.01f, 0.66f, 4));
                d.strokes.Add(Box(x0 + 0.095f, 0.42f, x0 + 0.125f, 0.54f));  // arrow slit
                d.strokes.Add(Box(x0 + 0.095f, 0.2f, x0 + 0.125f, 0.32f));
            }

            // curtain wall with battlements and the gate
            d.strokes.Add(Box(0.26f, 0.05f, 0.74f, 0.46f));
            d.strokes.Add(Battlements(0.26f, 0.74f, 0.46f, 5));
            d.strokes.Add(Polyline(true,
                new Vector2(0.42f, 0.05f), new Vector2(0.58f, 0.05f), new Vector2(0.58f, 0.22f), new Vector2(0.55f, 0.28f),
                new Vector2(0.5f, 0.3f), new Vector2(0.45f, 0.28f), new Vector2(0.42f, 0.22f)));
            foreach (float x in new[] { 0.46f, 0.5f, 0.54f })
                d.strokes.Add(Line(new Vector2(x, 0.06f), new Vector2(x, 0.27f)));           // portcullis bars
            d.strokes.Add(Line(new Vector2(0.43f, 0.16f), new Vector2(0.57f, 0.16f)));

            // tall central keep
            d.strokes.Add(Box(0.38f, 0.46f, 0.62f, 0.84f));
            d.strokes.Add(Battlements(0.37f, 0.63f, 0.84f, 3));
            d.strokes.Add(Box(0.485f, 0.62f, 0.515f, 0.74f));                               // keep window

            // a few stone courses
            foreach (var (x0, x1, y) in new[] { (0.3f, 0.4f, 0.36f), (0.62f, 0.7f, 0.3f), (0.42f, 0.5f, 0.56f), (0.09f, 0.17f, 0.12f), (0.82f, 0.9f, 0.6f) })
                d.strokes.Add(Line(new Vector2(x0, y), new Vector2(x1, y)));
            return d;
        }

        /// <summary>Castle level 2: sharpened wooden stakes at the foot of both towers (castle canvas space).</summary>
        public static Doodle CastleStakes()
        {
            var d = new Doodle();
            foreach (float x0 in new[] { -0.02f, 0.76f })
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = x0 + 0.035f + i * 0.065f;
                    float lean = (i - 1.5f) * 0.012f;
                    d.strokes.Add(Polyline(true, new Vector2(x - 0.02f, 0.03f), new Vector2(x + 0.02f, 0.03f),
                        new Vector2(x + lean, 0.17f)));
                }
                d.strokes.Add(Line(new Vector2(x0 + 0.01f, 0.09f), new Vector2(x0 + 0.25f, 0.09f))); // lashing rail
            }
            return d;
        }

        /// <summary>
        /// Castle levels 3 and 4: a cannon on the tower nearest the enemy, then one on the keep. Barrel tube,
        /// muzzle ring and a carriage wheel; mirrored for the castle on the right.
        /// </summary>
        public static Doodle CastleCannon(bool facingRight, bool onKeep)
        {
            var d = new Doodle();
            float x = onKeep ? 0.56f : 0.82f, y = onKeep ? 0.905f : 0.73f;
            d.strokes.Add(Capsule(x, x + 0.16f, y, 0.028f));                               // barrel
            d.strokes.Add(Circle(new Vector2(x + 0.165f, y), 0.034f));                   // muzzle ring
            d.strokes.Add(Circle(new Vector2(x + 0.03f, y - 0.03f), 0.036f));            // carriage wheel
            if (!facingRight)
                foreach (var s in d.strokes)
                    for (int i = 0; i < s.points.Count; i++)
                        s.points[i] = new Vector2(1f - s.points[i].x, s.points[i].y);
            return d;
        }

        /// <summary>Castle level 5: golden pennants flying from both towers.</summary>
        public static Doodle CastlePennants()
        {
            var d = new Doodle();
            foreach (float x in new[] { 0.08f, 0.92f })
            {
                d.strokes.Add(Line(new Vector2(x, 0.71f), new Vector2(x, 0.9f)));
                d.strokes.Add(Polyline(true, new Vector2(x, 0.9f), new Vector2(x + 0.1f, 0.865f), new Vector2(x, 0.83f)));
            }
            return d;
        }

        /// <summary>The banner on top of the keep, in the same canvas space as <see cref="Castle"/>.</summary>
        public static Doodle CastleBanner(InkColor color)
        {
            var d = new Doodle { color = color };
            d.strokes.Add(Line(new Vector2(0.5f, 0.9f), new Vector2(0.5f, 1.1f)));                   // pole
            d.strokes.Add(Polyline(true, new Vector2(0.5f, 1.1f), new Vector2(0.66f, 1.06f), new Vector2(0.62f, 1.03f),
                new Vector2(0.67f, 0.99f), new Vector2(0.5f, 1.0f)));                                  // swallow-tail flag
            return d;
        }

        /// <summary>A crenellated top edge: alternating merlons and gaps across [x0, x1] at height y.</summary>
        static Stroke Battlements(float x0, float x1, float y, int merlons)
        {
            var pts = new System.Collections.Generic.List<Vector2>();
            float step = (x1 - x0) / (merlons * 2 - 1);
            for (int i = 0; i < merlons * 2 - 1; i++)
            {
                float a = x0 + i * step, b = a + step;
                float top = i % 2 == 0 ? y + 0.06f : y;
                pts.Add(new Vector2(a, top));
                pts.Add(new Vector2(b, top));
                if (i < merlons * 2 - 2) pts.Add(new Vector2(b, i % 2 == 0 ? y : y + 0.06f));
            }
            pts.Insert(0, new Vector2(x0, y));
            pts.Add(new Vector2(x1, y));
            return Polyline(false, pts.ToArray());
        }

        internal static Stroke Line(Vector2 a, Vector2 b) => Polyline(false, a, b);

        internal static Stroke Polyline(bool closed, params Vector2[] corners)
        {
            var s = new Stroke();
            int count = closed ? corners.Length + 1 : corners.Length;
            s.points.Add(corners[0]);
            for (int i = 1; i < count; i++)
            {
                Vector2 a = corners[i - 1], b = corners[i % corners.Length];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.01f));
                for (int j = 1; j <= steps; j++) s.points.Add(Vector2.Lerp(a, b, (float)j / steps));
            }
            return s;
        }

        internal static Stroke Arc(Vector2 c, float rx, float ry, float fromDeg, float toDeg)
        {
            var s = new Stroke();
            const int segments = 20;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / segments) * Mathf.Deg2Rad;
                s.points.Add(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
            }
            return s;
        }

        internal static Stroke Circle(Vector2 c, float r) => Ellipse(c, r, r);

        /// <summary>A closed pill shape: straight top and bottom with round ends (a tank track).</summary>
        internal static Stroke Capsule(float x0, float x1, float cy, float r)
        {
            var s = new Stroke();
            const int arc = 12;
            for (int i = 0; i <= arc; i++) // right end, bottom to top
            {
                float a = Mathf.Lerp(-90f, 90f, (float)i / arc) * Mathf.Deg2Rad;
                s.points.Add(new Vector2(x1 + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            for (int i = 0; i <= arc; i++) // left end, top to bottom
            {
                float a = Mathf.Lerp(90f, 270f, (float)i / arc) * Mathf.Deg2Rad;
                s.points.Add(new Vector2(x0 + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            s.points.Add(s.points[0]);
            return s;
        }

        internal static Stroke Ellipse(Vector2 c, float rx, float ry)
        {
            var s = new Stroke();
            const int segments = 32;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                s.points.Add(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
            }
            return s;
        }
    }
}
