
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    static string[] INITIAL_BOARD_TEXT = {
        "abccd ",
        "abe df",
        "a ezzf",
        "gggh f",
        "  ihjj",
        "kkill "
    };

    static string[] INITIAL_BOARD_TEXT1 = {
        "abccdf",
        "abe df",
        "a ezzf",
        "gggh  ",
        "  ihjj",
        "kki ll"
    };

    const int BOARD_CX = 6;
    const int BOARD_CY = 6;

    static ConsoleColor[] COLOURS ={
        ConsoleColor.Black, // empty
        ConsoleColor.Red, // taxi
        ConsoleColor.Yellow,
        ConsoleColor.Green,
        ConsoleColor.Cyan,
        ConsoleColor.Blue,
        ConsoleColor.DarkCyan,  // pink
        ConsoleColor.Magenta,
        ConsoleColor.Blue,
        ConsoleColor.DarkMagenta,
        ConsoleColor.Gray,
        ConsoleColor.DarkGray, // brown
        ConsoleColor.DarkGreen,
        ConsoleColor.DarkYellow
    };

    static readonly string[] ANSI_COLOURS = {
        "\u001b[30m", // Black       
        "\u001b[38;2;139;0;0m", // Red         
        "\u001b[33m", // Yellow      
        "\u001b[32m", // Green       
        "\u001b[38;2;255;165;0m", //Orange
        "\u001b[36m", // Cyan        
        "\u001b[38;2;231;84;128m", // Pink
        "\u001b[35;1m", // Magenta    
        "\u001b[34m", // Blue   
        "\u001b[38;2;139;0;139m", // Dark Magenta
        "\u001b[38;2;0;100;0m", // Dark Green
        "\u001b[38;2;211;211;211m", // Gray
        "\u001b[38;2;181;101;29m", // Light Brown
        "\u001b[38;2;255;255;153m", // Light Yellow
    };

    const int VEHICLE_NONE = 0;
    const int VEHICLE_TAXI = 1;

    // when we have red taxi at these coordinates we've solved the puzzle
    const int ESCAPE_X = 5;
    const int ESCAPE_Y = 2;

    class Board
    {
        public int[,] Blocks { get; private set; } = new int[BOARD_CX, BOARD_CY];

        public Board ParentBoard { get; private set; } = null;

        public int Step { get; private set; } = 0;

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var y = 0; y < BOARD_CY; y++)
            {
                for (var x = 0; x < BOARD_CX; x++)
                {
                    sb.Append(Blocks[x, y].ToString("X"));
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
        var is1Move = IsOneMoveAwayFrom(CreateInitialBoard(INITIAL_BOARD_TEXT), CreateInitialBoard(INITIAL_BOARD_TEXT1));

        var initial = CreateInitialBoard(INITIAL_BOARD_TEXT);

        RenderBoard(0, initial);

        var solution = SolveGame(initial);

        RenderSolution(solution);
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

        List<Board> solution = new List<Board>();

        List<Board> encountered = new List<Board>();

        var p = 0;
        while (stack.Count() > 0)
        {
            var board = stack.Pop();
            encountered.Add(board);

            if (IsSolved(board))
            {
                var testSolution = GetPrunedSteps(board);
                Console.WriteLine($"Found solution using {testSolution.Count()} moves.");
                if ((solution.Count() == 0) || (testSolution.Count() < solution.Count()))
                {
                    solution = testSolution;
                }
                continue;
            }

            var newBoards = EnumerateNextBoards(board);

            ProcessBoards(stack, encountered, newBoards);

            p++;
            if ((p % 10000) == 0)
            {
                Console.WriteLine($"Iteration {p}, Stack {stack.Count()}");
            }
        }

        return solution;

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

                if (v == ' ')
                {
                    n = VEHICLE_NONE;
                }
                else if (v == 'z')
                {
                    n = VEHICLE_TAXI;
                }
                else
                {
                    n = v - ((int)'a') + 2;
                }
                board.Blocks[x, y] = n;
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
        var encountered = new List<int>();

        for (int y = 0; y < BOARD_CY; y++)
        {
            for (int x = 0; x < BOARD_CX; x++)
            {
                var current = board.Blocks[x, y];
                if (current == VEHICLE_NONE)
                {
                    // skip empty blocks
                    continue;
                }

                // if we've already processed this vehicle, skip
                if (encountered.Contains(current))
                {
                    continue;
                }
                encountered.Add(current);

                // determine whether to scan to the left/right or updards/downwards.
                // all vehicles are at least 2 blocks long and since we are scanning top down
                // and left right, we know whether the vehicle is horizontal or vertical
                // by testing the block to the right and the block below
                var dx = x + 1 < BOARD_CX ? (board.Blocks[x + 1, y] == current ? 1 : 0) : 0;
                var dy = y + 1 < BOARD_CY ? (board.Blocks[x, y + 1] == current ? 1 : 0) : 0;

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
            adjacent = board.Blocks[x, y];
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
                var isChanged = board1.Blocks[x, y] != board2.Blocks[x, y];
                if (!isChanged)
                {
                    continue;
                }

                var vehicle1 = board1.Blocks[x, y];
                var vehicle2 = board2.Blocks[x, y];

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
        return board.Blocks[ESCAPE_X, ESCAPE_Y] == VEHICLE_TAXI;
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
                var v = board.Blocks[x, y];
                if (v != VEHICLE_NONE)
                {
                    if (v == vehicle)
                    {
                    }

                    newBoard.Blocks[x + (v == vehicle ? dx : 0), y + (v == vehicle ? dy : 0)] = v;
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
        for (var y = 0; y < BOARD_CY; y++)
        {
            for (var x = 0; x < BOARD_CX; x++)
            {
                if (board1.Blocks[x, y] != board2.Blocks[x, y])
                {
                    return false;
                }
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
                var v = board.Blocks[x, y];
                if (v == VEHICLE_NONE)
                {
                    Console.Write("   ");
                    continue;
                }

                //Console.ForegroundColor = COLOURS[v]; // doesn't work on integrated vscode terminal on mac
                Console.Write(ANSI_COLOURS[v % ANSI_COLOURS.Length]);
                Console.Write("▉▉ ");
            }
            Console.WriteLine();
        }

        Console.WriteLine();
    }

}