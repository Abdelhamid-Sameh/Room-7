using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Game Boy's snake maze, with no scene objects in the logic. A perfect maze with a few shortcuts knocked
/// through (a snake cannot turn round, so it needs loops), four apples, and an exit in the east wall that stays
/// shut until every apple is eaten. Hitting a wall or yourself starts the run again. Y grows downwards, like the picture.
/// Render() paints a 160 x 144 picture in the four greens of the original screen.
/// </summary>
public class SnakeMazeGame
{
    public const int W = 19, H = 15;                 // cells, walls included
    public const int ScreenW = 160, ScreenH = 144;
    public const int AppleCount = 4;
    private const int Cell = 8, OffX = 4, OffY = 18;

    private static readonly Color32 P0 = new Color32(155, 188, 15, 255);
    private static readonly Color32 P1 = new Color32(139, 172, 15, 255);
    private static readonly Color32 P2 = new Color32(48, 98, 48, 255);
    private static readonly Color32 P3 = new Color32(15, 56, 15, 255);

    private static readonly string[] AppleArt = { "..##..", ".####.", "######", "######", ".####.", "..##.." };
    private static readonly string[] ArrowArt = { "..#...", "..##..", "######", "..##..", "..#..." };

    public static readonly Vector2Int Up = new Vector2Int(0, -1), Down = new Vector2Int(0, 1), Left = new Vector2Int(-1, 0), Right = new Vector2Int(1, 0);

    public readonly bool[,] wall = new bool[W, H];
    public readonly Vector2Int start = new Vector2Int(1, 1);
    public readonly Vector2Int exit = new Vector2Int(W - 1, H - 2);
    public readonly Vector2Int[] apples = new Vector2Int[AppleCount];

    private readonly bool[] eaten = new bool[AppleCount];
    private readonly List<Vector2Int> body = new List<Vector2Int>();
    private Vector2Int dir, heading;
    private int grow;

    public bool Started { get; private set; }
    public bool Dead { get; private set; }
    public bool Won { get; private set; }
    public int ApplesLeft { get { int n = 0; for (int i = 0; i < AppleCount; i++) if (!eaten[i]) n++; return n; } }
    public int Length { get { return body.Count; } }

    public event Action OnMove, OnEat, OnCrash, OnWin;

    public SnakeMazeGame(int seed = 11)
    {
        System.Random rng = new System.Random(seed);
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) wall[x, y] = true;

        // depth-first carving over the 9 x 7 rooms (odd cells), two cells at a time
        Vector2Int[] dirs = { Right, Left, Up, Down };
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        wall[start.x, start.y] = false;
        stack.Push(start);
        while (stack.Count > 0)
        {
            Vector2Int cur = stack.Peek();
            List<Vector2Int> options = new List<Vector2Int>();
            foreach (Vector2Int d in dirs)
            {
                Vector2Int n = cur + d * 2;
                if (n.x > 0 && n.y > 0 && n.x < W - 1 && n.y < H - 1 && wall[n.x, n.y]) options.Add(d);
            }
            if (options.Count == 0) { stack.Pop(); continue; }
            Vector2Int pick = options[rng.Next(options.Count)];
            Vector2Int between = cur + pick, next = cur + pick * 2;
            wall[between.x, between.y] = false;
            wall[next.x, next.y] = false;
            stack.Push(next);
        }

        // shortcuts: knock out some of the walls that sit between two rooms
        for (int x = 1; x < W - 1; x++)
            for (int y = 1; y < H - 1; y++)
                if (wall[x, y] && ((x % 2 == 0) != (y % 2 == 0)) && rng.NextDouble() < 0.2) wall[x, y] = false;

        // no dead ends: a snake cannot turn round, so a dead end would always be the end of it
        for (int x = 1; x < W - 1; x += 2)
            for (int y = 1; y < H - 1; y += 2)
            {
                List<Vector2Int> shut = new List<Vector2Int>();
                int open = 0;
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int b = new Vector2Int(x, y) + d, n = new Vector2Int(x, y) + d * 2;
                    if (!wall[b.x, b.y]) open++;
                    else if (n.x > 0 && n.y > 0 && n.x < W - 1 && n.y < H - 1) shut.Add(b);
                }
                if (open < 2 && shut.Count > 0) { Vector2Int b = shut[rng.Next(shut.Count)]; wall[b.x, b.y] = false; }
            }

        wall[exit.x, exit.y] = false;
        PlaceApples();
        Reset();
    }

    private void PlaceApples()
    {
        // greedy farthest-point picks over the rooms, so the apples are spread right across the maze
        List<Vector2Int> picked = new List<Vector2Int> { start };
        Vector2Int exitRoom = new Vector2Int(W - 2, H - 2);
        for (int i = 0; i < AppleCount; i++)
        {
            Vector2Int best = start;
            int bestScore = -1;
            for (int x = 1; x < W - 1; x += 2)
                for (int y = 1; y < H - 1; y += 2)
                {
                    Vector2Int r = new Vector2Int(x, y);
                    if (r == exitRoom || picked.Contains(r)) continue;
                    int score = int.MaxValue;
                    foreach (Vector2Int p in picked) score = Mathf.Min(score, Mathf.Abs(p.x - x) + Mathf.Abs(p.y - y));
                    if (score > bestScore) { bestScore = score; best = r; }
                }
            apples[i] = best;
            picked.Add(best);
        }
    }

    /// <summary>A fresh run: one segment at the start (it grows by two as it moves), every apple back.</summary>
    public void Reset()
    {
        body.Clear();
        body.Add(start);
        dir = heading = Right;
        grow = 2;
        Started = Dead = Won = false;
        for (int i = 0; i < AppleCount; i++) eaten[i] = false;
    }

    public void Turn(Vector2Int d)
    {
        if (Dead || Won) return;
        if (!Started) { Started = true; dir = heading = d; return; }
        if (d != -dir) heading = d;   // no turning straight back on yourself
    }

    public void Step()
    {
        if (!Started || Dead || Won) return;
        dir = heading;
        Vector2Int n = body[0] + dir;

        if (n == exit)
        {
            if (ApplesLeft == 0) { body.Insert(0, n); Won = true; if (OnWin != null) OnWin(); }
            else Crash();
            return;
        }

        bool solid = n.x < 0 || n.y < 0 || n.x >= W || n.y >= H || wall[n.x, n.y];
        bool self = false;
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] != n) continue;
            self = !(i == body.Count - 1 && grow == 0);   // the tail steps out of the way
            break;
        }
        if (solid || self) { Crash(); return; }

        body.Insert(0, n);
        if (grow > 0) grow--; else body.RemoveAt(body.Count - 1);

        for (int k = 0; k < AppleCount; k++)
            if (!eaten[k] && apples[k] == n) { eaten[k] = true; grow += 2; if (OnEat != null) OnEat(); }
        if (OnMove != null) OnMove();
    }

    private void Crash()
    {
        Dead = true;
        if (OnCrash != null) OnCrash();
    }

    // ------------------------------------------------------------------ picture --

    public void Render(Color32[] px, float time, bool flash)
    {
        for (int i = 0; i < px.Length; i++) px[i] = P0;

        // status bar: the apples, ticked off as they go
        for (int i = 0; i < AppleCount; i++) Sprite(px, AppleArt, 6 + i * 10, 5, eaten[i] ? P3 : P2, !eaten[i]);
        if (ApplesLeft == 0 && !Won && ((int)(time * 3f) % 2 == 0)) Sprite(px, ArrowArt, 146, 5, P3, false);

        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                int px0 = OffX + x * Cell, py0 = OffY + y * Cell;
                if (new Vector2Int(x, y) == exit)
                {
                    if (ApplesLeft > 0) { Fill(px, px0, py0, Cell, Cell, P3); for (int k = 0; k < 8; k += 2) for (int j = 0; j < 8; j += 2) Set(px, px0 + k, py0 + j, P1); }
                    else if ((int)(time * 3f) % 2 == 0) Sprite(px, ArrowArt, px0 + 1, py0 + 1, P3, false);
                }
                else if (wall[x, y])
                {
                    Fill(px, px0, py0, Cell, Cell, P3);
                    Fill(px, px0 + 1, py0 + 1, Cell - 2, Cell - 2, P2);
                }
            }

        for (int k = 0; k < AppleCount; k++)
            if (!eaten[k]) Sprite(px, AppleArt, OffX + apples[k].x * Cell + 1, OffY + apples[k].y * Cell + 1, ((int)(time * 2.5f) % 2 == 0) ? P3 : P2, false);

        for (int i = body.Count - 1; i >= 0; i--)
        {
            int bx = OffX + body[i].x * Cell, by = OffY + body[i].y * Cell;
            if (i == 0)
            {
                if (!Started && (int)(time * 4f) % 2 != 0) continue;   // waiting for the first move: the head blinks
                Fill(px, bx, by, Cell, Cell, P3);
                // two eyes on the leading side
                int ex = dir.x > 0 ? 5 : dir.x < 0 ? 2 : -1, ey = dir.y > 0 ? 5 : dir.y < 0 ? 2 : -1;
                if (dir.x != 0) { Set(px, bx + ex, by + 2, P0); Set(px, bx + ex, by + 5, P0); }
                else { Set(px, bx + 2, by + ey, P0); Set(px, bx + 5, by + ey, P0); }
            }
            else Fill(px, bx + 1, by + 1, Cell - 2, Cell - 2, P3);
        }

        if (flash)
            for (int i = 0; i < px.Length; i++) px[i] = Invert(px[i]);
    }

    private static Color32 Invert(Color32 c)
    {
        if (c.r == P0.r && c.g == P0.g) return P3;
        if (c.r == P3.r && c.g == P3.g) return P0;
        if (c.r == P1.r && c.g == P1.g) return P2;
        return P1;
    }

    private static void Set(Color32[] px, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= ScreenW || y >= ScreenH) return;
        px[(ScreenH - 1 - y) * ScreenW + x] = c;
    }

    private static void Fill(Color32[] px, int x, int y, int w, int h, Color32 c)
    {
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Set(px, x + i, y + j, c);
    }

    private static void Sprite(Color32[] px, string[] art, int x, int y, Color32 c, bool checker)
    {
        for (int j = 0; j < art.Length; j++)
            for (int i = 0; i < art[j].Length; i++)
                if (art[j][i] == '#' && (!checker || (i + j) % 2 == 0)) Set(px, x + i, y + j, c);
    }
}
