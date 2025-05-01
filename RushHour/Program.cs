
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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

    const string VEHICLE_CODES = " XOABCDPQEFGHI";

    const int VEHICLE_NONE = 0;
    const int VEHICLE_TAXI = 1;
    const int VEHICLE_MAX = 20;


    // when we have red taxi at these coordinates we've solved the puzzle
    const int ESCAPE_X = 5;
    const int ESCAPE_Y = 2;


    const int BOARD_CX = 6;
    const int BOARD_CY = 6;

    static readonly string[] ANSI_BG_COLOURS = {
        "\u001b[40m", // Black (background)
        "\u001b[48;2;139;0;0m", // Red (background)
        "\u001b[43m", // Yellow (background)
        "\u001b[42m", // Green (background)
        "\u001b[48;2;255;165;0m", // Orange (background)
        "\u001b[46m", // Cyan (background)
        "\u001b[48;2;231;84;128m", // Pink (background)
        "\u001b[45;1m", // Magenta (bright) background
        "\u001b[44m", // Blue (background)
        "\u001b[48;2;139;0;139m", // Dark Magenta (background)
        "\u001b[48;2;0;100;0m", // Dark Green (background)
        "\u001b[48;2;211;211;211m", // Gray (background)
        "\u001b[48;2;181;101;29m", // Light Brown (background)
        "\u001b[48;2;255;255;153m", // Light Yellow (background)
    };

    class Board
    {
        public int[] Blocks { get; private set; } = new int[BOARD_CX * BOARD_CY];

        public Board? ParentBoard { get; private set; } = null;

        public int Step { get; private set; } = 0;

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var y = 0; y < BOARD_CY; y++)
            {
                for (var x = 0; x < BOARD_CX; x++)
                {
                    sb.Append(Blocks[y * BOARD_CX + x].ToString("X"));
                }
                sb.Append("|");
            }

            return sb.ToString();
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
    /// Attempts to solve the game and returns the best solution (i.e. the one with the
    /// fewest steps taken)
    /// </summary>
    /// <param name="initialBoard"></param>
    /// <returns></returns>
    static List<Board> SolveGame(Board initialBoard)
    {
        var stack = new Stack<Board>();

        stack.Push(initialBoard);

        List<Board> bestSolution = new List<Board>();

        List<Board> encountered = new List<Board>();

        var p = 0;
        while (stack.Count() > 0)
        {
            var board = stack.Pop();
            encountered.Add(board);

            if (IsSolved(board))
            {
                var testSolution = GetPrunedSteps(board);
                Console.WriteLine($"Found solution using {testSolution.Count()} moves, stack is {stack.Count}.");
                if ((bestSolution.Count() == 0) || (testSolution.Count() < bestSolution.Count()))
                {
                    bestSolution = testSolution;
                }
                continue;
            }

            var newBoards = EnumerateNextBoards(board);

            ProcessBoards(stack, encountered, newBoards);

            p++;
            if ((p % 10000) == 0)
            {
                Console.WriteLine($"Iteration {p}, Stack {stack.Count()}, Encountered {encountered.Count} unique states.");
            }
        }

        return bestSolution;

    }

    /// <summary>
    /// Iterates throguh a set of potential boards and loads them into the stack if they are a
    /// unique state (i.e. a configuration that has not yet been seen)
    /// </summary>
    /// <param name="stack"></param>
    /// <param name="encountered"></param>
    /// <param name="newBoards"></param>
    static void ProcessBoards(Stack<Board> stack, List<Board> encountered, IEnumerable<Board> newBoards)
    {
        foreach (var newBoard in newBoards)
        {
            // if the board is identical to an antecedent, exclude it
            if (IsBoardIdenticalToAntecedent(newBoard))
            {
                continue;
            }

            // if the boad is identical to any other board we've already seen, exclude it
            if (IsBoardIdenticalTo(newBoard, encountered))
            {
                continue;
            }

            stack.Push(newBoard);
        }
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
                board.Blocks[y * BOARD_CX + x] = n;
            }
        }

        return board;

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
                var current = board.Blocks[y * BOARD_CX + x];
                if (current == VEHICLE_NONE)
                {
                    // skip empty blocks
                    continue;
                }

                // if we've already processed this vehicle, skip
                if (encountered[current])
                {
                    continue;
                }
                encountered[current] = true;

                // determine whether to scan to the left/right or updards/downwards.
                // all vehicles are at least 2 blocks long and since we are scanning top down
                // and left right, we know whether the vehicle is horizontal or vertical
                // by testing the block to the right and the block below. note that this 
                // just determines the orientation of the vehicle not whether there
                // is actually space for it to move
                var dx = x + 1 < BOARD_CX ? (board.Blocks[y * BOARD_CX + x + 1] == current ? 1 : 0) : 0;
                var dy = y + 1 < BOARD_CY ? (board.Blocks[(y + 1) * BOARD_CX + x] == current ? 1 : 0) : 0;

                // test for delta +1 - i.e. right or down
                var canMovePos = CanMove(board, current, x, y, dx, dy);
                if (canMovePos)
                {
                    var newBoard = CloneBoardMoveVehicle(board, current, dx, dy);
                    yield return newBoard;
                }

                // test for delta -1 - i.e. left or up
                var canMoveNeg = CanMove(board, current, x, y, -dx, -dy);
                if (canMoveNeg)
                {
                    var newBoard = CloneBoardMoveVehicle(board, current, -dx, -dy);
                    yield return newBoard;
                }
            }
        }
    }

    /// <summary>
    /// Returns true if the given vehicle can be moved (dx, dy)
    /// </summary>
    /// <param name="board"></param>
    /// <param name="vehicle"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="dx"></param>
    /// <param name="dy"></param>
    /// <returns></returns>
    static bool CanMove(Board board, int vehicle, int x, int y, int dx, int dy)
    {
        if ((dx == 0) && (dy == 0))
        {
            return false;
        }

        // find the next adjacent cell that isn't this vehicle
        var adjacent = vehicle;
        while (adjacent == vehicle)
        {
            adjacent = board.Blocks[y * BOARD_CX + x];
            if (adjacent == VEHICLE_NONE)
            {
                return true;
            }

            x += dx;
            y += dy;

            if ((x < 0) || (x >= BOARD_CX) || (y < 0) || (y >= BOARD_CY))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if board2 is 1 valid step away from board1 (i.e. a single
    /// move would change the state of board1 so that it is equivalent to board2)
    /// </summary>
    /// <param name="board1"></param>
    /// <param name="board2"></param>
    /// <returns></returns>
    static bool IsOneMoveAwayFrom(Board board1, Board board2)
    {
        var movedVehicle = VEHICLE_NONE;
        var moveCount = 0;

        for (var y = 0; y < BOARD_CY; y++)
        {
            for (var x = 0; x < BOARD_CX; x++)
            {
                var isChanged = board1.Blocks[y * BOARD_CX + x] != board2.Blocks[y * BOARD_CX + x];
                if (!isChanged)
                {
                    continue;
                }

                var vehicle1 = board1.Blocks[y * BOARD_CX + x];
                var vehicle2 = board2.Blocks[y * BOARD_CX + x];

                var testVehicle = vehicle1 == VEHICLE_NONE ? vehicle2 : vehicle1;

                // if we have already identified which vehicle was moved in a previous block,
                // the vehicle in this block must match
                if ((movedVehicle != VEHICLE_NONE) && (testVehicle != movedVehicle))
                {
                    return false;
                }

                movedVehicle = testVehicle;
                moveCount++;
                if (moveCount > 2)
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
    /// Return true if the board is identical to any board it is descended from
    /// </summary>
    /// <param name="board"></param>
    /// <returns></returns>
    static bool IsBoardIdenticalToAntecedent(Board board)
    {
        Board parentBoard = board.ParentBoard;
        while (parentBoard != null)
        {
            if (IsBoardIdenticalTo(board, parentBoard))
            {
                return true;
            }
            parentBoard = parentBoard.ParentBoard;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the board has an identical state to any of the other boards in others
    /// </summary>
    /// <param name="self"></param>
    /// <param name="others"></param>
    /// <returns></returns>
    static bool IsBoardIdenticalTo(Board self, List<Board> others)
    {
        foreach (var other in others)
        {
            if (IsBoardIdenticalTo(self, other))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if the two boards contain identical state
    /// </summary>
    /// <param name="board1"></param>
    /// <param name="board2"></param>
    /// <returns></returns>
    static bool IsBoardIdenticalTo(Board board1, Board board2)
    {
        // expect that for an array this small, this is faster than options like SequenceEqual
        var b1 = board1.Blocks;
        var b2 = board2.Blocks;
        for (var i = 0; i < BOARD_CY * BOARD_CX; i++)
        {
            if (b1[i] != b2[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the required steps to achieve a solution
    /// </summary>
    /// <param name="finalStep"></param>
    /// <returns></returns>
    static List<Board> GetPrunedSteps(Board finalStep)
    {
        if (finalStep == null)
        {
            return null;
        }

        var currentStep = finalStep;
        var steps = new List<Board>();
        while (currentStep != null)
        {
            steps.Add(currentStep);
            currentStep = currentStep.ParentBoard;
        }

        steps.Reverse();

        var prunedSteps = new List<Board>();

        var i = 0;
        while (i < steps.Count() - 1)
        {
            prunedSteps.Add(steps[i]);

            // walk backwards from the last step to the current step to find the highest 
            // step that represents a single change from the current step. we can safely
            // prune all the intermediate steps because they're not actually progressing 
            // the solution. for example, they're unneccessary oscillations of vehicles
            for (var j = steps.Count() - 1; j > i; j--)
            {
                var isOneMove = IsOneMoveAwayFrom(steps[i], steps[j]);
                if (isOneMove)
                {
                    i = j;
                    break;
                }
            }
        }

        // add the last step
        prunedSteps.Add(steps[steps.Count() - 1]);

        return prunedSteps;
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