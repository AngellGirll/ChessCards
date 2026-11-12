using System;
using UnityEngine;

namespace ChessCards
{
    public sealed partial class ChessCardsApp
    {
        static readonly Color YardBurgundy = Hex("591D39"), YardVelvet = Hex("4E1531"),
            YardGround = Hex("291123"), YardWood = Hex("80482D"), YardPrice = Hex("65463A"),
            YardComplete = Hex("BEE6A0");
        Texture2D yardCrown, yardCrest, yardCorner, yardVelvetTexture, yardWoodTexture, yardFade;

        void LoadCourtyardStyle(string resourceRoot)
        {
            yardCrown = Resources.Load<Texture2D>(resourceRoot + "YardCrown");
            yardCrest = Resources.Load<Texture2D>(resourceRoot + "YardCrest");
            yardCorner = Resources.Load<Texture2D>(resourceRoot + "YardCorner");
            yardVelvetTexture = CourtyardSurface(YardVelvet, false);
            yardWoodTexture = CourtyardSurface(YardWood, true);

            yardFade = new Texture2D(1, 48, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var fade = new Color[48];
            for (int i = 0; i < fade.Length; i++)
            {
                float t = 1 - i / (fade.Length - 1f);
                float alpha = t * t * (3 - 2 * t);
                fade[i] = new Color(YardGround.r, YardGround.g, YardGround.b, alpha);
            }
            yardFade.SetPixels(fade);
            yardFade.Apply(false, true);
        }

        static Texture2D CourtyardSurface(Color color, bool wood)
        {
            const int width = 96, height = 48;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            var grain = new System.Random(wood ? 64 : 21);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float light = (y / (height - 1f) - .55f) * .15f;
                light += ((float)grain.NextDouble() - .5f) * .024f;
                if (wood)
                {
                    double ripple = Math.Sin(x * .16 + y * .33) * 1.1 + Math.Sin(x * .07) * 1.5;
                    light += (float)Math.Sin(y * 1.2 + ripple) * .040f;
                }
                pixels[y * width + x] = light >= 0
                    ? Color.Lerp(color, Color.white, light)
                    : Color.Lerp(color, Color.black, -light);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void DrawLocation()
        {
            int repaired = 0;
            for (int i = 0; i < RestorationProgress.Prices.Length; i++)
                if (progress.Repaired(i)) repaired++;

            Fill(new Rect(0, 0, w, h), YardGround);
            DrawCourtyardScene(new Rect(0, 0, w, portrait ? 805 : h));
            if (portrait)
                Rounded(new Rect(0, 555, w, 250), Color.white, 0, yardFade);
            DrawCourtyardHeader();
            DrawRestorationPanel(portrait ? new Rect(32, 690, 656, 393) : new Rect(834, 115, 414, 445), repaired);
            DrawCourtyardFooter(repaired);
        }

        void DrawCourtyardScene(Rect scene)
        {
            if (courtyard == null) return;
            bool allRepaired = progress.Bench && progress.Lamp && progress.House;
            Texture2D art = allRepaired && restoredCourtyard != null ? restoredCourtyard : courtyard;
            float scale = Math.Max(scene.width / art.width, scene.height / art.height);
            float width = art.width * scale, height = art.height * scale;
            // In portrait, keep the lantern and bench in the visible center of the crop.
            float cropAnchor = portrait ? .44f : .5f;
            Rect image = new Rect((scene.width - width) * cropAnchor, (scene.height - height) * .5f, width, height);

            GUI.BeginGroup(scene);
            GUI.DrawTexture(image, art);
            if (!allRepaired && restoredCourtyard != null)
            {
                if (progress.House) DrawRestoredArea(scene, image, new Rect(.405f, .018f, .49f, .49f));
                if (progress.Bench) DrawRestoredArea(scene, image, new Rect(.243f, .444f, .247f, .310f));
                if (progress.Lamp) DrawRestoredArea(scene, image, new Rect(.329f, .195f, .090f, .180f));
            }
            GUI.EndGroup();
        }

        void DrawRestoredArea(Rect scene, Rect image, Rect patch)
        {
            float left = Math.Max(0, image.x + patch.x * image.width);
            float top = Math.Max(0, image.y + patch.y * image.height);
            float right = Math.Min(scene.width, image.x + (patch.x + patch.width) * image.width);
            float bottom = Math.Min(scene.height, image.y + (patch.y + patch.height) * image.height);
            if (right <= left || bottom <= top) return;
            GUI.BeginGroup(new Rect(left, top, right - left, bottom - top));
            GUI.DrawTexture(new Rect(image.x - left, image.y - top, image.width, image.height), restoredCourtyard);
            GUI.EndGroup();
        }

        void DrawCourtyardHeader()
        {
            Rect title = new Rect(portrait ? 44 : 58, 25, portrait ? 407 : 511, portrait ? 62 : 66);
            Framed(title, YardBurgundy, 22, 4);
            var titleText = new Rect(title.x + 34, title.y, title.width - 57, title.height);
            YardText(titleText, T("Королевский двор", "Royal Courtyard"), portrait ? 28 : 37, TrimLight, true);
            if (yardCrown != null)
                GUI.DrawTexture(new Rect(28, 16, portrait ? 59 : 73, portrait ? 77 : 82), yardCrown, ScaleMode.ScaleToFit);

            YardText(new Rect(50, portrait ? 91 : 99, 238, 33),
                T("Побед: ", "Wins: ") + progress.Wins, portrait ? 23 : 21, ink, false);
            var wallet = new Rect(w - (portrait ? 253 : 258), 25, portrait ? 150 : 155, 55);
            Framed(wallet, YardBurgundy, 19, 3);
            DrawCoin(new Rect(wallet.x + 10, wallet.y + 7, 40, 40));
            Label(new Rect(wallet.x + 52, wallet.y, wallet.width - 62, wallet.height),
                progress.Coins.ToString(), 25, ink, true, true);
            if (Button(new Rect(w - 91, 25, 64, 55), "⚙", YardBurgundy, true, 29)) settingsOpen = true;
        }

        void DrawRestorationPanel(Rect panelRect, int repaired)
        {
            Framed(panelRect, YardBurgundy, 24, 5);
            Rounded(Inset(panelRect, 6), Color.white, 18, yardVelvetTexture);
            Rect heading = new Rect(panelRect.x + 7, panelRect.y + 7, panelRect.width - 14, portrait ? 54 : 59);
            Rounded(heading, Color.white, 16, yardWoodTexture);
            Fill(new Rect(heading.x + 12, heading.y + heading.height - 1, heading.width - 24, 1), Hex("BE7841"));
            YardText(heading, T("Восстановление", "Restoration"), portrait ? 32 : 29, ink, true);

            if (yardCrest != null)
                GUI.DrawTexture(new Rect(panelRect.x + panelRect.width / 2 - 55, panelRect.y - 27, 110, 38),
                    yardCrest, ScaleMode.ScaleToFit);
            DrawYardCorner(new Rect(panelRect.x - 1, panelRect.y - 1, 45, 45), false, false);
            DrawYardCorner(new Rect(panelRect.x + panelRect.width - 44, panelRect.y - 1, 45, 45), true, false);
            DrawYardCorner(new Rect(panelRect.x - 1, panelRect.y + panelRect.height - 44, 45, 45), false, true);
            DrawYardCorner(new Rect(panelRect.x + panelRect.width - 44, panelRect.y + panelRect.height - 44, 45, 45), true, true);

            int total = RestorationProgress.Prices.Length;
            string count = repaired + T(" из ", " of ") + total;
            Label(new Rect(panelRect.x + 26, panelRect.y + 65, panelRect.width - 52, 27), count, 22, ink, true, true);
            var track = new Rect(panelRect.x + 27, panelRect.y + 99, panelRect.width - 54, 17);
            Rounded(track, TrimDark, 8);
            Rounded(Inset(track, 2), YardGround, 6);
            if (repaired > 0)
            {
                var fill = new Rect(track.x + 3, track.y + 3, (track.width - 6) * repaired / total, track.height - 6);
                Rounded(fill, Color.white, 5, Gloss(Trim));
            }

            float rowHeight = portrait ? 82 : 100;
            for (int i = 0; i < total; i++)
            {
                var row = new Rect(panelRect.x + 19, panelRect.y + 126 + i * rowHeight, panelRect.width - 38, rowHeight);
                if (i != 0)
                    Fill(new Rect(row.x + 3, row.y, row.width - 6, 1), new Color(.86f, .61f, .62f, .31f));
                DrawRepairRow(row, i);
            }
        }

        void DrawYardCorner(Rect r, bool right, bool bottom)
        {
            if (yardCorner != null)
                GUI.DrawTextureWithTexCoords(r, yardCorner, new Rect(right ? 1 : 0, bottom ? 1 : 0,
                    right ? -1 : 1, bottom ? -1 : 1));
        }

        string RepairName(int index)
        {
            if (index == 0) return T("Скамейка", "Bench");
            return index == 1 ? T("Фонарь", "Lantern") : T("Дом", "Cottage");
        }

        void DrawRepairRow(Rect row, int index)
        {
            Texture2D icon = repairArt[index];
            float iconSize = portrait ? 74 : 78;
            if (icon != null)
                GUI.DrawTexture(new Rect(row.x + 7, row.y + (row.height - iconSize) / 2, iconSize, iconSize),
                    icon, ScaleMode.ScaleToFit);
            float actionWidth = portrait ? 150 : 111;
            var action = new Rect(row.x + row.width - actionWidth - 5, row.y + (row.height - 44) / 2, actionWidth, 44);
            Label(new Rect(row.x + 92, row.y, row.width - actionWidth - 105, row.height),
                RepairName(index), portrait ? 28 : 24, ink, false, true);

            if (progress.Repaired(index))
            {
                Label(action, T("✓ Готово", "✓ Restored"), portrait ? 22 : 19, YardComplete, true, true);
                return;
            }

            int cost = RestorationProgress.Prices[index];
            if (progress.Coins >= cost)
            {
                if (Button(action, "", Green) && progress.Repair(index))
                {
                    lastRepair = index;
                    Save();
                }
            }
            else Rounded(action, YardPrice, 13);

            DrawCoin(new Rect(action.x + 12, action.y + 8, 28, 28));
            Label(new Rect(action.x + 43, action.y, action.width - 50, action.height),
                cost.ToString(), 23, ink, true, true);
        }

        void DrawCourtyardFooter(int repaired)
        {
            string notice = "";
            if (repaired == RestorationProgress.Prices.Length)
                notice = T("Двор восстановлен!", "Courtyard restored!");
            else if (lastRepair >= 0)
                notice = RepairName(lastRepair) + T(" • ремонт завершён!", " • repair complete!");
            else if (showLastResult && match != null)
                notice = match.Winner == 0 ? T("Победа! +100 монет на ремонт.", "You win! +100 coins for repairs.") : match.GetResult(English);
            if (notice.Length != 0)
                YardText(portrait ? new Rect(34, 1085, 652, 37) : new Rect(834, 573, 414, 61),
                    notice, portrait ? 20 : 18, ink, true);

            var play = portrait ? new Rect(104, 1130, 512, 68) : new Rect(220, 575, 380, 73);
            string label = T("Играть", "Play") + (preferences.Selection == ModeSelection.Random ? T("  •  Рандом", "  •  Random") : "  ▶");
            if (Button(play, label, Green, true, portrait ? 30 : 33))
            {
                PlayFromCourtyard();
                GUIUtility.ExitGUI();
            }
            DrawYardCorner(new Rect(play.x - 3, play.y - 4, 43, 44), false, false);
            DrawYardCorner(new Rect(play.x + play.width - 40, play.y - 4, 43, 44), true, false);
            YardText(new Rect(play.x - 15, play.y + play.height + 8, play.width + 30, 33),
                T("+100 монет за победу", "+100 coins per win"), portrait ? 23 : 22, ink, true);
        }

        void YardText(Rect r, string value, int size, Color color, bool centered)
        {
            Label(new Rect(r.x + 1, r.y + 2, r.width, r.height), value, size, Shadow, centered, true);
            Label(r, value, size, color, centered, true);
        }
    }
}
