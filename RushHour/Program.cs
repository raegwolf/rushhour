﻿using System.Diagnostics;

class Program
{
    static string[] INITIAL_BOARD_TEXT = { // layout card 32 (highest official solve steps)
        "OOOAPQ",
        "BCCAPQ",
        "B XXPQ",
        "DDE   ",
        " FEGG ",
        " FHHII"
    };

    static string[] INITIAL_BOARD_TEXT_38 = { // layout card 38 (seems hard)
        "OABBC ",
        "OAD CP",
        "O DXXP",
        "QQQE P",
        "  FEGG",
        "HHFII "
    };

    static string[] INITIAL_BOARD_TEXT_40 = { // layout card 40 (last card)
        "ABBOOO",
        "A CCD ",
        "XXE D ",
        "FFEGGP",
        " HHI P",
        "QQQI P"
    };

    const string VEHICLE_CODES = " XABCDEFGHIJKOPQR";

    const int VEHICLE_NONE = 0;
    const int VEHICLE_TAXI = 1;
    const int VEHICLE_MAX = 20;


    // when we have red taxi at these coordinates we've solved the puzzle
    const int ESCAPE_X = 5;
    const int ESCAPE_Y = 2;


    const int BOARD_CX = 6;
    const int BOARD_CY = 6;

    const int BOARD_MAX_MOVE = 4;// max blocks for a move

    static readonly string[] ANSI_BG_COLOURS = {
        "\u001b[40m", // Black (background)
        "\u001b[48;2;139;0;0m", // Red (background)
        "\u001b[42m", // Green (background)
        "\u001b[48;2;255;165;0m", // Orange (background)
        "\u001b[46m", // Cyan (background)
        "\u001b[48;2;231;84;128m", // Pink (background)
        "\u001b[48;2;75;0;130m", // Indigo (background)
        "\u001b[48;2;0;100;0m", // Dark Green (background)
        "\u001b[48;2;211;211;211m", // Light Gray (background)
        "\u001b[48;2;181;101;29m", // Light Brown (background)
        "\u001b[48;2;255;255;153m", // Light Yellow (background)
        "\u001b[48;2;101;67;33m", // Dark Brown (background)
        "\u001b[48;2;80;120;90m", // Pond Green (background)
        "\u001b[43m", // Yellow (background)
        "\u001b[48;2;139;0;139m", // Dark Magenta (background)
        "\u001b[44m", // Blue (background)
        "\u001b[48;2;0;168;107m", // Jade (background)
        "\u001b[48;2;0;168;107m" // Jade (background)
    };

    class Board
    {
        public byte[] Blocks { get; private set; } = new byte[BOARD_CX * BOARD_CY];

        public Board? ParentBoard { get; private set; } = null;

        public int Step { get; private set; } = 0;

        public override string ToString()
        {
            return Convert.ToBase64String(Blocks);
        }

        public Board(Board parentBoard)
        {
            ParentBoard = parentBoard;
            if (parentBoard != null)
            {
                Step = parentBoard.Step + 1;
            }
        }
    }

    static void Main()
    {
        var initial = CreateInitialBoard(INITIAL_BOARD_TEXT);

        RenderBoard(0, initial);

        var stopwatch = Stopwatch.StartNew();

        var solution = SolveGame(initial);

        stopwatch.Stop();

        RenderSolution(solution);

        Console.WriteLine($"Solved in {stopwatch.ElapsedMilliseconds / 1000.00}s.");
    }

    /// <summary>
    /// Returns a Board object from a text representation of a board
    /// </summary>
    /// <param name="boardText"></param>
    /// <returns></returns>
    static Board CreateInitialBoard(string[] boardText)
    {
        var board = new Board(null);

        for (var y = 0; y < BOARD_CY; y++)
        {
            for (var x = 0; x < BOARD_CX; x++)
            {
                var n = 0;
                var v = boardText[y][x];

                n = VEHICLE_CODES.IndexOf(v);
                if (n == -1)
                {
                    throw new Exception("Invalid vehicle.");
                }
                board.Blocks[y * BOARD_CX + x] = (byte)n;
            }
        }

        return board;
    }

    /// <summary>
    /// Attempts to solve the game and returns the best solution (i.e. the one with the
    /// fewest steps taken)
    /// </summary>
    /// <param name="initialBoard"></param>
    /// <returns></returns>
    static List<Board> SolveGame(Board initialBoard)
    {
        var queue = new Queue<Board>();

        queue.Enqueue(initialBoard);

        var bestSolution = new List<Board>();

        var encountered = new Dictionary<string, int>();

        var p = 0;
        while (queue.Count() > 0)
        {
            var board = queue.Dequeue();

            if (IsSolved(board))
            {
                var testSolution = GetStepsToBoard(board);
                if ((bestSolution.Count() == 0) || (testSolution.Count() < bestSolution.Count()))
                {
                    Console.WriteLine($"Found new best solution using {testSolution.Count()} moves.");
                    bestSolution = testSolution;
                }
                continue;
            }

            var newBoards = EnumerateNextBoards(board);

            foreach (var newBoard in newBoards)
            {
                var newBoardKey = newBoard.ToString();

                // if we've already seen this board state with fewer steps, there is no need to
                // process this board
                if (encountered.ContainsKey(newBoardKey) && (encountered[newBoardKey] <= newBoard.Step))
                {
                    continue;
                }

                encountered[newBoard.ToString()] = newBoard.Step;

                queue.Enqueue(newBoard);
            }

            p++;
            if ((p % 10000) == 0)
            {
                Console.WriteLine($"Processed {p} unique states, queue size is {queue.Count()}, step depth is {board.Step}.");
            }
        }

        return bestSolution;

    }

    /// <summary>
    /// Returns all boards that represent valid next states (i.e. each one contains a valid move)
    /// </summary>
    /// <param name="board"></param>
    /// <returns></returns>
    static IEnumerable<Board> EnumerateNextBoards(Board board)
    {
        var encountered = new bool[VEHICLE_MAX];

        for (int y = 0; y < BOARD_CY; y++)
        {
            for (int x = 0; x < BOARD_CX; x++)
            {
                var vehicle = board.Blocks[y * BOARD_CX + x];
                if (vehicle == VEHICLE_NONE)
                {
                    // skip empty blocks
                    continue;
                }

                // if we've already processed this vehicle, skip
                if (encountered[vehicle])
                {
                    continue;
                }
                encountered[vehicle] = true;

                // determine vehicle orientation
                var isHorizontal = (x + 1 < BOARD_CX) && (board.Blocks[y * BOARD_CX + x + 1] == vehicle);

                var moves = GetAvailableMoves(
                    board,
                    vehicle,
                    isHorizontal ? BOARD_MAX_MOVE : 0,
                    isHorizontal ? 0 : BOARD_MAX_MOVE);

                foreach (var move in moves)
                {
                    yield return CloneBoardMoveVehicle(board, vehicle, move.dx, move.dy);
                }
            }
        }
    }

    static IEnumerable<(int dx, int dy)> GetAvailableMoves(Board board, byte vehicle, int maxX, int maxY)
    {
        var moves = new List<(int dx, int dy)>();

        moves.AddRange(GetAvailableMoves(
            board,
            vehicle,
            maxX == 0 ? 0 : 1,
            maxY == 0 ? 0 : 1,
            maxX,
            maxY));
        moves.AddRange(GetAvailableMoves(
            board,
            vehicle,
            maxX == 0 ? 0 : -1,
            maxY == 0 ? 0 : -1,
            -maxX,
            -maxY));

        return moves;
    }

    static IEnumerable<(int dx, int dy)> GetAvailableMoves(Board board, byte vehicle, int dx, int dy, int tx, int ty)
    {

        var cx = dx;
        var cy = dy;

        do
        {
            if (!IsValidMove(board, vehicle, cx, cy))
            {
                yield break;
            }

            yield return (cx, cy);

            cx += dx;
            cy += dy;
        } while ((cx != tx)| (cy != ty));

    }

    static bool IsValidMove(Board board, byte vehicle, int dx, int dy)
    {
        for (var y = 0; y < BOARD_CY; y++)
        {
            for (var x = 0; x < BOARD_CX; x++)
            {
                var v = board.Blocks[y * BOARD_CX + x];

                // if the current block is not the vehicle, ignore it
                if (v != vehicle)
                {
                    continue;
                }

                // if the block is out of bounds, it's not valid
                if ((x + dx < 0) ||
                    (x + dx >= BOARD_CX) ||
                    (y + dy < 0) ||
                   (y + dy >= BOARD_CY))
                {
                    return false;
                }

                // if the target block is not either empty or the same vehicle, it's not valid
                var tv = board.Blocks[(y + dy) * BOARD_CX + (x + dx)];

                if ((tv != VEHICLE_NONE) && (tv != vehicle))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Returns true if the board is the final step of a solution
    /// </summary>
    /// <param name="board"></param>
    /// <returns></returns>
    static bool IsSolved(Board board)
    {
        return board.Blocks[ESCAPE_Y * BOARD_CX + ESCAPE_X] == VEHICLE_TAXI;
    }

    /// <summary>
    /// Creates a copy of the board where the target vehicle is moved (dx,dy)
    /// </summary>
    /// <param name="board"></param>
    /// <param name="vehicle"></param>
    /// <param name="dx"></param>
    /// <param name="dy"></param>
    /// <returns></returns>
    static Board CloneBoardMoveVehicle(Board board, int vehicle, int dx, int dy)
    {
        // we have an empty space, emit a board and shift
        // the current vehicle in the direction of the empty
        // space
        var newBoard = new Board(board);

        for (var y = 0; y < BOARD_CY; y++)
        {
            for (var x = 0; x < BOARD_CX; x++)
            {
                var v = board.Blocks[y * BOARD_CX + x];
                if (v != VEHICLE_NONE)
                {
                    var nx = x + (v == vehicle ? dx : 0);
                    var ny = y + (v == vehicle ? dy : 0);
                    newBoard.Blocks[ny * BOARD_CX + nx] = v;
                }
            }
        }

        return newBoard;
    }

    /// <summary>
    /// Returns the required steps to achieve a solution
    /// </summary>
    /// <param name="finalStep"></param>
    /// <returns></returns>
    static List<Board> GetStepsToBoard(Board finalStep)
    {
        var board = finalStep;
        List<Board> steps = new();

        while (board != null)
        {
            steps.Add(board);
            board = board.ParentBoard;
        }
        steps.Reverse();
        return steps;
    }

    /// <summary>
    /// Renders all the steps that were taken to achieve a solution
    /// </summary>
    /// <param name="solution"></param>
    static void RenderSolution(List<Board> solution)
    {
        if ((solution == null) || (solution.Count() == 0))
        {
            Console.WriteLine("No solution was found.");
            return;
        }

        var i = 1;
        foreach (var step in solution)
        {
            RenderBoard(i, step);
            i++;
        }
        Console.WriteLine($"Found solution containing {solution.Count} steps.");
    }

    /// <summary>
    /// Renders an ASCII version of the board
    /// </summary>
    /// <param name="board"></param>
    static void RenderBoard(int step, Board board)
    {

        Console.WriteLine($"Step {step}");

        for (var y = 0; y < BOARD_CY; y++)
        {
            for (var x = 0; x < BOARD_CX; x++)
            {
                var v = board.Blocks[y * BOARD_CX + x];
                if (v == VEHICLE_NONE)
                {
                    Console.Write("\u001b[48;2;32;32;32m   \u001b[0m");
                    continue;
                }
                Console.Write(ANSI_BG_COLOURS[v % ANSI_BG_COLOURS.Length]);
                var letter = VEHICLE_CODES[v];
                Console.Write($"{letter}{letter}");

                Console.Write("\u001b[48;2;32;32;32m \u001b[0m");
            }
            Console.WriteLine();
        }

        Console.WriteLine();
    }

}