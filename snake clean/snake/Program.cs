using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;

namespace snake
{
    public static class PointExtensions
    {
        public static Point Add(this Point point, Game.Direction direction)
        {
            var result = new Point(point.X, point.Y);
            switch (direction)
            {
                case Game.Direction.Right:
                    result.X++;
                    break;
                case Game.Direction.Down:
                    result.Y++;
                    break;
                case Game.Direction.Left:
                    result.X--;
                    break;
                case Game.Direction.Up:
                    result.Y--;
                    break;
            }
            return result;
        }

        public static Point Add(this Point point1, Point point2)
        {
            var result = new Point(point1.X + point2.X, point1.Y + point2.Y);
            return result;
        }

        public static Point Sub(this Point point, Game.Direction direction)
        {
            var result = new Point(point.X, point.Y);
            switch (direction)
            {
                case Game.Direction.Right:
                    result.X--;
                    break;
                case Game.Direction.Down:
                    result.Y--;
                    break;
                case Game.Direction.Left:
                    result.X++;
                    break;
                case Game.Direction.Up:
                    result.Y++;
                    break;
            }
            return result;
        }

        public static Point Sub(this Point point1, Point point2)
        {
            var result = new Point(point1.X - point2.X, point1.Y - point2.Y);
            return result;
        }
    }

    public class Game
    {
        public enum State
        {
            Empty,
            Fruit,
            Snake
        }

        public enum Direction
        {
            Right,
            Down,
            Left,
            Up
        }

        private class Waypoint
        {
            public int Cost;
            public Direction PreviousMove;
        }

        public int MapWidth { get; private set; }
        public int MapHeight { get; private set; }
        public bool MapWraparound { get; set; }
        public int GameSpeed { get; private set; }
        private Point headLocation { get; set; }
        public State[,] Map { get; private set; }
        private Waypoint[,] wayPoints { get; set; }
        private Queue<Point> pointQueue { get; set; }
        private Stack<Direction> path { get; set; }
        private Point autoTail { get; set; }
        private Queue<Direction> autoInputs { get; set; }
        private Queue<Point> snake { get; set; }
        public int Score { get { return snake.Count; } }
        public Direction SnakeDirection { get; private set; }
        private Direction WrongDirection { get; set; }
        //private Point fruit { get; set; }
        private Random random { get; set; }
        public bool GameOver { get; set; }

        public event Action<int, int, State> StateChanged;
        public event Action<int> SpeedChanged;
        public event Action<int> ScoreChanged;
        public event Action GameOverEvent;

        public Game(int mapWidth = 8, int mapHeight = 8, int gameSpeed = 1, int startX = 0, int startY = 0)
        {
            this.MapWidth = mapWidth;
            this.MapHeight = mapHeight;
            if (!Inbound(startX, startY) || mapWidth < 2 || mapHeight < 2) throw new IndexOutOfRangeException();
            changeSpeed(gameSpeed);
            headLocation = new Point(startX, startY);
            Map = new State[mapWidth, mapHeight];
            snake = new Queue<Point>();
            snake.Enqueue(new Point(headLocation.X, headLocation.Y));
            SnakeDirection = Direction.Right;
            changeState(startX, startY, State.Snake);
            random = new Random();
            spawnFruit();
            GameOver = false;
            MapWraparound = false;
            autoInputs = new Queue<Direction>();
            autoTail = headLocation;
        }

        public bool Inbound(int x, int y)
        {
            return (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight);
        }

        public bool Inbound(Point point)
        {
            return Inbound(point.X, point.Y);
        }

        public Point WrapPoint(int x, int y)
        {
            return WrapPoint(new Point(x, y));
        }

        public Point WrapPoint (Point point)
        {
            return new Point((point.X + MapWidth) % MapWidth, (point.Y + MapHeight) % MapHeight);
        }

        private void changeState (int x, int y, State state)
        {
            if (!Inbound(x, y)) return;
            Map[x, y] = state;
            if (StateChanged != null) StateChanged(x, y, state);
        }

        private void changeSpeed (int speed)
        {
            if (speed <= 0 || speed > 1000) return;
            GameSpeed=speed;
            if (SpeedChanged != null) SpeedChanged(GameSpeed);
        }

        private void endGame()
        {
            GameOver = true;
            if (GameOverEvent != null) GameOverEvent();
        }

        public void Move()
        {
            headLocation = headLocation.Add(SnakeDirection);
            if (MapWraparound) headLocation = WrapPoint(headLocation);
            WrongDirection = (Direction)(((int)SnakeDirection + 2) % 4);

            snake.Enqueue(headLocation);
            if (!Inbound(headLocation) || Map[headLocation.X, headLocation.Y] == State.Snake)
            {
                var tailLocation = snake.Dequeue();
                if (headLocation != tailLocation)
                {
                    endGame();
                    return;
                }
            }
            else if (Map[headLocation.X, headLocation.Y] == State.Empty)
            {
                var tailLocation = snake.Dequeue();
                changeState(tailLocation.X, tailLocation.Y, State.Empty);
            }
            else
            {
                spawnFruit();
                if (ScoreChanged != null) ScoreChanged(Score);
                if ((snake.Count) % 5 == 0)
                    changeSpeed(GameSpeed + 1);
            }
            changeState(headLocation.X, headLocation.Y, State.Snake);
            if (snake.Count == MapHeight * MapWidth)
                endGame();
        }

        public void autoMove()
        {
            var nextMove = Direction.Up;
            if (path == null)
            {
                if (!pathFind(headLocation))
                {
                    var nextPoint = headLocation.Add(nextMove);
                    var twoPointsAhead = nextPoint.Add(nextMove);
                    if (MapWraparound)
                    {
                        nextPoint = WrapPoint(nextPoint);
                        twoPointsAhead = WrapPoint(twoPointsAhead);
                    }
                    if (!Inbound(nextPoint) || Map[nextPoint.X, nextPoint.Y] == State.Snake && nextPoint != autoTail ||
                        !Inbound(twoPointsAhead) || Map[twoPointsAhead.X, twoPointsAhead.Y] == State.Snake && twoPointsAhead != autoTail)
                    {
                        nextMove = Direction.Down;
                        nextPoint = headLocation.Add(nextMove);
                        if (MapWraparound) nextPoint = WrapPoint(nextPoint);
                        if (!Inbound(nextPoint) || Map[nextPoint.X, nextPoint.Y] == State.Snake && nextPoint != autoTail)
                        {
                            nextMove = Direction.Right;
                            nextPoint = headLocation.Add(nextMove);
                            if (MapWraparound) nextPoint = WrapPoint(nextPoint);
                            if (!Inbound(nextPoint) || Map[nextPoint.X, nextPoint.Y] == State.Snake && nextPoint != autoTail)
                            {
                                nextMove = Direction.Left;
                                nextPoint = headLocation.Add(nextMove);
                                if (MapWraparound) nextPoint = WrapPoint(nextPoint);
                                if (!Inbound(nextPoint) || Map[nextPoint.X, nextPoint.Y] == State.Snake && nextPoint != autoTail)
                                    nextMove = Direction.Up;
                            }
                        }
                    }
                    if ((WrongDirection == Direction.Left || WrongDirection == Direction.Right) && nextMove == Direction.Down)
                    {
                        var abovePoint = headLocation.Add(Direction.Up);
                        if (MapWraparound) abovePoint = WrapPoint(abovePoint);
                        twoPointsAhead = headLocation.Sub(WrongDirection);
                        if (MapWraparound) twoPointsAhead = WrapPoint(twoPointsAhead);
                        if ((!Inbound(twoPointsAhead) || Map[twoPointsAhead.X, twoPointsAhead.Y] == State.Snake && twoPointsAhead != autoTail) && 
                            (Inbound(abovePoint) && (Map[abovePoint.X, abovePoint.Y] != State.Snake || abovePoint == autoTail)))
                            nextMove = Direction.Up;
                    }
                }
            }
            if (path != null)
            {
                nextMove = path.Pop();
                if (path.Count == 0)
                    path = null;
            }
            ChangeDirection(nextMove);
            autoInputs.Enqueue(nextMove);
            Move();
            if (snake.Count == autoInputs.Count)
                autoTail = autoTail.Add(autoInputs.Dequeue());
        }

        private bool pathFind( Point start)
        {
            wayPoints = new Waypoint[MapWidth, MapHeight];
            path = null;
            pointQueue = new Queue<Point>();
            wayPoints[start.X, start.Y] = new Waypoint();
            pointQueue.Enqueue(new Point(start.X, start.Y));
            while (pointQueue.Count > 0)
            {
                var currentPoint = pointQueue.Dequeue();
                var currentCost = wayPoints[currentPoint.X, currentPoint.Y].Cost;
                if (Map[currentPoint.X, currentPoint.Y] == State.Fruit)
                {
                    path = new Stack<Direction>();
                    while (wayPoints[currentPoint.X, currentPoint.Y].Cost != 0)
                    {
                        path.Push(wayPoints[currentPoint.X, currentPoint.Y].PreviousMove);
                        currentPoint = currentPoint.Sub(wayPoints[currentPoint.X, currentPoint.Y].PreviousMove);
                        if (MapWraparound) currentPoint = WrapPoint(currentPoint);
                    }
                    return true;
                }
                    for (var move = Direction.Right; move <= Direction.Up; move++)
                    {
                            var nextPoint = currentPoint.Add(move);
                            if (MapWraparound) nextPoint = WrapPoint(nextPoint);
                            var nextCost = currentCost + 1;
                            if (nextPoint.X == 0 || nextPoint.X == MapWidth - 1 ||
                                nextPoint.Y == 0 || nextPoint.Y == MapHeight - 1)
                                nextCost++;
                            if (Inbound(nextPoint) &&
                                (wayPoints[nextPoint.X, nextPoint.Y] == null || wayPoints[nextPoint.X, nextPoint.Y].Cost > nextCost) &&
                                (Map[nextPoint.X, nextPoint.Y] != State.Snake || nextPoint == autoTail))
                            {
                                pointQueue.Enqueue(nextPoint);
                                wayPoints[nextPoint.X, nextPoint.Y] = new Waypoint { Cost = nextCost, PreviousMove = move };
                            }
                        }
            }
            return false;
        }

        private void spawnFruit()
        {
            if (snake.Count >= MapWidth * MapHeight) return;
            int x = random.Next(0, MapWidth - 1); ;
            int y = random.Next(0, MapHeight - 1);
            for (var j = 0; j < MapHeight; j++)
                for (var i = 0; i < MapWidth; i++)
                    if (Map[(x + i) % MapWidth, (y + j) % MapHeight] == State.Empty)
                    {
                        //Map[(x + i) % MapWidth, (y + j) % MapHeight] = State.Fruit;
                        changeState((x + i) % MapWidth, (y + j) % MapHeight, State.Fruit);
                        return;
                    }
        }

        public void ChangeDirection(Direction direction)
        {
            if (snake.Count == 1 || direction != WrongDirection)
                SnakeDirection = direction;
        }

    }

    /////////////////////////////////////////////////////////////////////////
    
    class Program
    {
        class GameForm : Form
        {
            private int infoWidth;
            private int fieldWidth;
            private int fieldHeight;
            private Game game;
            private int mapWidth;
            private int mapHeight;
            private int infoRows;
            private Label speedLabel;
            private Label scoreLabel;
            private Label gameOverLabel;
            private TextBox mapWidthInput;
            private TextBox mapHeightInput;
            private CheckBox wraparoundCheckbox;
            private bool nonNumberEntered;
            private Button startButton;
            private Button demoButton;
            private bool playingDemo;
            private Label highScoreLabel;
            private float cellSide;
            private System.Windows.Forms.Timer timer;
            private int highScore;
            private SolidBrush brush;
            private Pen pen;

            public GameForm(int fieldHeight = 400, int infoWidth = 200, int mapWidth = 10, int mapHeight = 10)
            {
                if (infoWidth <= 0 || fieldHeight <= 0 || mapWidth < 2 || mapHeight < 2) throw new ArgumentOutOfRangeException();
                this.infoWidth = infoWidth;
                this.fieldHeight = fieldHeight;
                this.infoRows = 7;
                this.fieldWidth = (int)(fieldHeight / mapHeight) * mapWidth;
                this.DoubleBuffered = true;
                brush = new SolidBrush(Color.Red);
                pen = new Pen(Color.DimGray, 2);

                setMapSize(mapWidth, mapHeight);

                this.BackColor = Color.SlateGray;

                speedLabel = new Label();
                speedLabel.TextAlign = ContentAlignment.MiddleCenter;
                speedLabel.ForeColor = Color.Black;
                speedLabel.BackColor = Color.LightGray;

                scoreLabel = new Label();
                scoreLabel.TextAlign = speedLabel.TextAlign;
                scoreLabel.ForeColor = speedLabel.ForeColor;
                scoreLabel.BackColor = speedLabel.BackColor;

                highScoreLabel = new Label();
                highScoreLabel.TextAlign = speedLabel.TextAlign;
                highScoreLabel.ForeColor = speedLabel.ForeColor;
                highScoreLabel.BackColor = speedLabel.BackColor;

                startButton = new Button();
                startButton.ForeColor = Color.Black;
                startButton.BackColor = Color.White;
                startButton.Text = "Start";
                startButton.Click += startGame;

                gameOverLabel = new Label();
                gameOverLabel.TextAlign = speedLabel.TextAlign;
                gameOverLabel.ForeColor = speedLabel.ForeColor;
                gameOverLabel.BackColor = speedLabel.BackColor;

                mapWidthInput = new TextBox();
                mapWidthInput.TextAlign = HorizontalAlignment.Center;
                mapWidthInput.ForeColor = Color.Black;
                mapWidthInput.BackColor = Color.White;
                mapWidthInput.Text = mapWidth.ToString();

                mapHeightInput = new TextBox();
                mapHeightInput.TextAlign = mapWidthInput.TextAlign;
                mapHeightInput.ForeColor = mapWidthInput.ForeColor;
                mapHeightInput.BackColor = mapWidthInput.BackColor;
                mapHeightInput.Text = mapHeight.ToString();

                wraparoundCheckbox = new CheckBox();
                wraparoundCheckbox.TextAlign = ContentAlignment.MiddleLeft;
                wraparoundCheckbox.ForeColor = Color.Black;
                wraparoundCheckbox.Text = "Map Wraparound";

                demoButton = new Button();
                demoButton.TextAlign = startButton.TextAlign;
                demoButton.ForeColor = startButton.ForeColor;
                demoButton.BackColor = startButton.BackColor;
                demoButton.Text = "Start Demo";
                demoButton.Click += startDemo;

                timer = new System.Windows.Forms.Timer();
                timer.Tick += onTick;

                Controls.Add(speedLabel);
                Controls.Add(scoreLabel);
                Controls.Add(gameOverLabel);
                Controls.Add(startButton);
                Controls.Add(highScoreLabel);
                Controls.Add(mapWidthInput);
                Controls.Add(mapHeightInput);
                Controls.Add(wraparoundCheckbox);
                Controls.Add(demoButton);

                this.Resize += resizeElements;
                this.KeyDown += updateInput;
                this.KeyPreview = true;
                this.Paint += (sender, args)=> updateOnPaint(args.Graphics);
                mapWidthInput.KeyDown += mapSizeInput;
                mapHeightInput.KeyDown += mapSizeInput;
                mapWidthInput.KeyPress += mapSizeKeyPress;
                mapHeightInput.KeyPress += mapSizeKeyPress;

                Size = new Size(fieldWidth + infoWidth + (this.Width - this.ClientSize.Width),
                    fieldHeight + (this.Height - this.ClientSize.Height));
                this.Text = "Snake";
                this.Icon = Properties.Resources.SnakeIcon;
            }

            private void resizeElements(object sender, EventArgs args)
            {
                if (this.WindowState == FormWindowState.Minimized) return;

                if (mapHeight * 10 >= (infoRows * 3 + 1) * 10 || mapHeight >= mapWidth)
                {
                    this.Height = Math.Max(Math.Max(mapHeight * 10, (infoRows * 3 + 1) * 10) + this.Height - this.ClientSize.Height, this.Height);
                    this.fieldHeight = this.ClientSize.Height;
                    this.cellSide = (float)this.fieldHeight / (float)mapHeight;
                    this.fieldWidth = (int)(cellSide * mapWidth);
                    this.infoWidth = Math.Max(this.ClientSize.Width - fieldWidth, 100);
                    this.Width = fieldWidth + infoWidth + this.Width - this.ClientSize.Width;
                }
                else
                {
                    infoWidth = Math.Max(10 * this.ClientSize.Height / (infoRows * 3 + 1), 100);
                    this.Height = Math.Max(this.Height - this.ClientSize.Height + (infoRows * 3 + 1) * infoWidth / 10, this.Height);
                    this.fieldHeight = this.ClientSize.Height;
                    this.fieldWidth = Math.Max(this.ClientSize.Width - infoWidth, mapWidth * 10);
                    this.Width = this.fieldWidth + infoWidth + this.Width - this.ClientSize.Width;
                    this.cellSide = Math.Min((float)fieldWidth / (float)mapWidth, (float)fieldHeight / (float)mapHeight);
                }

                Invalidate();

                var labelTopBorder = (int)Math.Min(fieldHeight / (infoRows * 3 + 1), infoWidth / 10);

                speedLabel.Size = new Size(labelTopBorder * 8, labelTopBorder * 2);
                speedLabel.Location = new Point((int)(this.fieldWidth + (infoWidth - speedLabel.Size.Width) / 2), (int)(labelTopBorder));
                speedLabel.Font = new Font("Calibri", (int)(speedLabel.Height * 0.4));

                scoreLabel.Size = speedLabel.Size;
                scoreLabel.Location = new Point(speedLabel.Left, speedLabel.Bottom + labelTopBorder);
                scoreLabel.Font = speedLabel.Font;

                highScoreLabel.Size = speedLabel.Size;
                highScoreLabel.Location = new Point(scoreLabel.Left, scoreLabel.Bottom + labelTopBorder);
                highScoreLabel.Font = speedLabel.Font;

                gameOverLabel.Size = speedLabel.Size;
                gameOverLabel.Location = new Point(highScoreLabel.Left, highScoreLabel.Bottom + labelTopBorder);
                gameOverLabel.Font = speedLabel.Font;

                mapWidthInput.Size = new Size ((speedLabel.Size.Width-labelTopBorder) / 2, speedLabel.Size.Height);
                mapWidthInput.Location = new Point(gameOverLabel.Left, gameOverLabel.Bottom + labelTopBorder);
                mapWidthInput.Font = speedLabel.Font;

                mapHeightInput.Size = mapWidthInput.Size;
                mapHeightInput.Location = new Point(mapWidthInput.Right + labelTopBorder, mapWidthInput.Top);
                mapHeightInput.Font = speedLabel.Font;

                wraparoundCheckbox.Size = new Size(speedLabel.Width, labelTopBorder);
                wraparoundCheckbox.Location = new Point(mapWidthInput.Left, mapWidthInput.Bottom);
                wraparoundCheckbox.Font = new Font("Calibri", (int)(wraparoundCheckbox.Height * 0.4));

                startButton.Size = speedLabel.Size;
                startButton.Location = new Point(mapWidthInput.Left, mapWidthInput.Bottom + labelTopBorder);
                startButton.Font = speedLabel.Font;

                demoButton.Size = speedLabel.Size;
                demoButton.Location = new Point(startButton.Left, startButton.Bottom + labelTopBorder);
                demoButton.Font = speedLabel.Font;
            }

            private void setMapSize (int mapWidth, int mapHeight)
            {
                if (mapWidth < 2 || mapHeight < 2 || mapWidth > 100 || mapHeight > 100) throw new ArgumentOutOfRangeException();
                this.mapWidth = mapWidth;
                this.mapHeight = mapHeight;
            }

            private void startGame(object sender, EventArgs args)
            {
                var newMapWidth = int.Parse(mapWidthInput.Text);
                var newMapHeight = int.Parse(mapHeightInput.Text);
                if (game != null && wraparoundCheckbox.Checked != game.MapWraparound) game = null;
                if (newMapWidth != mapWidth || newMapHeight != mapHeight)
                {
                    newMapWidth = newMapWidth < 2 ? 2 : newMapWidth > 100 ? 100 : newMapWidth;
                    mapWidthInput.Text = newMapWidth.ToString();
                    newMapHeight = newMapHeight < 2 ? 2 : newMapHeight > 100 ? 100 : newMapHeight;
                    mapHeightInput.Text = newMapHeight.ToString();
                    setMapSize(newMapWidth, newMapHeight);
                    game = null;
                    resizeElements(new object(), new EventArgs());
                }

                highScore = game == null ? 0 : game.GameOver && !playingDemo ? Math.Max(highScore, game.Score) : highScore;
                highScoreLabel.Text = "High Score: " + highScore;

                playingDemo = false;

                game = new Game(mapWidth, mapHeight);
                game.MapWraparound = wraparoundCheckbox.Checked;
                game.StateChanged += (x, y, state) => Invalidate();
                game.SpeedChanged += updateSpeed;
                game.ScoreChanged += updateScore;
                game.GameOverEvent += updateGameOver;
                startButton.Text = "Restart";
                demoButton.Text = "Start Demo";
                timer.Stop();
                updateView();
                pauseGame();
            }

            private void startDemo(object sender, EventArgs args)
            {
                startGame(new Object(), new EventArgs());
                playingDemo = true;
                gameOverLabel.Text = "Demo";
                startButton.Text = "Start";
                demoButton.Text = "Restart Demo";
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (timer.Enabled && !playingDemo)
                    switch (keyData)
                    {
                        case Keys.Right:
                            game.ChangeDirection(Game.Direction.Right);
                            return true;
                            break;
                        case Keys.Down:
                            game.ChangeDirection(Game.Direction.Down);
                            return true;
                            break;
                        case Keys.Left:
                            game.ChangeDirection(Game.Direction.Left);
                            return true;
                            break;
                        case Keys.Up:
                            game.ChangeDirection(Game.Direction.Up);
                            return true;
                            break;
                    }

                return base.ProcessCmdKey(ref msg, keyData);
            }

            private void updateInput(object sender, KeyEventArgs args)
            {
                switch (args.KeyData)
                {
                    case Keys.R:
                        startGame(sender, args);
                        break;
                    case Keys.F:
                        this.WindowState = (this.WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized;
                        break;
                    case Keys.P:
                    case Keys.Escape:
                        pauseGame();
                        break;
                }

                if (timer.Enabled && !playingDemo)
                    switch (args.KeyData)
                    {
                        case Keys.D:
                            game.ChangeDirection(Game.Direction.Right);
                            break;
                        case Keys.S:
                            game.ChangeDirection(Game.Direction.Down);
                            break;
                        case Keys.A:
                            game.ChangeDirection(Game.Direction.Left);
                            break;
                        case Keys.W:
                            game.ChangeDirection(Game.Direction.Up);
                            break;
                    }
            }

            private void mapSizeInput(object sender, KeyEventArgs args)
            {
                nonNumberEntered = false;

                if (args.KeyData == Keys.Enter)
                    startGame(new Object(), new EventArgs());

                if (args.KeyCode < Keys.D0 || args.KeyCode > Keys.D9)
                {
                    if (args.KeyCode < Keys.NumPad0 || args.KeyCode > Keys.NumPad9)
                    {
                        if (args.KeyCode != Keys.Back)
                        {
                            nonNumberEntered = true;
                        }
                    }
                }
                if (Control.ModifierKeys == Keys.Shift)
                {
                    nonNumberEntered = true;
                }
            }

            private void mapSizeKeyPress(object sender, System.Windows.Forms.KeyPressEventArgs args)
            {
                if (nonNumberEntered == true)
                {
                    args.Handled = true;
                }
            }

            private void pauseGame()
            {
                if (game != null)
                    if (timer.Enabled)
                    {
                        timer.Stop();
                        mapWidthInput.Enabled = true;
                        mapHeightInput.Enabled = true;
                        wraparoundCheckbox.Enabled = true;
                        gameOverLabel.Text = "Pause";
                    }
                    else if (!game.GameOver)
                    {
                        timer.Start();
                        mapWidthInput.Enabled = false;
                        mapHeightInput.Enabled = false;
                        wraparoundCheckbox.Enabled = false;
                        gameOverLabel.Text = playingDemo ? "Demo" : "";
                    }
            }

            private void onTick (object sender, EventArgs args)
            {
                if (!playingDemo)
                    game.Move();
                else
                    game.autoMove();
            }

            private void drawCell (int x, int y, Game.State state, Graphics e)
            {
                //SolidBrush brush = new SolidBrush(Color.Red);
                //Pen pen = new Pen(Color.Black, 1);
                //var posX = x * picSize.Width + pen.Width / 2;
                //var posY = y * picSize.Height + pen.Width / 2;
                //pen.Width = (int)(picSize.Width * 0.05);
                switch (state)
                {
                    case Game.State.Empty:
                        brush.Color = Color.Beige;
                        break;
                    case Game.State.Snake:
                        brush.Color = Color.Green;
                        break;
                    case Game.State.Fruit:
                        brush.Color = Color.Orange;
                        break;
                }

                e.FillRectangle(brush, x * cellSide, y * cellSide, cellSide, cellSide);
                e.DrawRectangle(pen, x * cellSide, y * cellSide, cellSide, cellSide);
            }

            private void updateOnPaint(Graphics e)
            {
                for (var j = 0; j < mapHeight; j++)
                {
                    for (var i = 0; i < mapWidth; i++)
                    {
                        if (game != null)
                            drawCell(i, j, game.Map[i, j], e);
                        else
                            drawCell(i, j, Game.State.Empty, e);
                    }
                }
            }

            private void updateSpeed(int speed)
            {
                speedLabel.Text = "Speed: " + speed;
                timer.Interval = 1000 / speed;
            }

            private void updateScore(int score)
            {
                scoreLabel.Text = "Score: " + score;
            }

            private void updateGameOver()
            {
                pauseGame();
                gameOverLabel.Text = "Game Over!";
                startButton.Text = "Start";
                demoButton.Text = "Start Demo";
            }

            private void updateView()
            {
                Invalidate();

                updateSpeed(game.GameSpeed);
                updateScore(game.Score);
            }
        }

        static void Main()
        {
            //ConsoleGame();
            WindowsFormsGame();
        }

        /////////////////////////////////////////////////////////////////////////

        private static void WindowsFormsGame()
        {
            Application.Run(new GameForm());
        }

        /////////////////////////////////////////////////////////////////////////

        private static void ConsoleGame()
        {
            var game = new Game(10,10);
            ConsoleRenderer(game);
            game.StateChanged += (x, y, state) =>
            {
                Console.SetCursorPosition(x + 1, y + 1);
                switch (state)
                {
                    case Game.State.Empty:
                        Console.Write(" ");
                        break;
                    case Game.State.Snake:
                        Console.Write("X");
                        break;
                    case Game.State.Fruit:
                        Console.Write("O");
                        break;
                }
                Console.SetCursorPosition(0, game.MapHeight + 2);
            };
            game.SpeedChanged += (speed) =>
            {
                Console.SetCursorPosition(game.MapWidth + 2, 1);
                Console.Write(" Game speed: " + game.GameSpeed);
                Console.SetCursorPosition(0, game.MapHeight + 2);
            };
            game.ScoreChanged += (score) =>
            {
                Console.SetCursorPosition(game.MapWidth + 2, 2);
                Console.Write(" Score: " + game.Score);
                Console.SetCursorPosition(0, game.MapHeight + 2);
            };
            game.GameOverEvent += () => 
            {
                Console.SetCursorPosition(0, game.MapHeight + 2);
                Console.Write(" Game Over!");
            };
            while (!game.GameOver)
            {
                int timeOutMS = 1000 / game.GameSpeed;
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var input = Console.ReadKey(true);
                        switch (input.Key)
                        {
                            case ConsoleKey.RightArrow:
                            case ConsoleKey.D:
                                game.ChangeDirection(Game.Direction.Right);
                                break;
                            case ConsoleKey.DownArrow:
                            case ConsoleKey.S:
                                game.ChangeDirection(Game.Direction.Down);
                                break;
                            case ConsoleKey.LeftArrow:
                            case ConsoleKey.A:
                                game.ChangeDirection(Game.Direction.Left);
                                break;
                            case ConsoleKey.UpArrow:
                            case ConsoleKey.W:
                                game.ChangeDirection(Game.Direction.Up);
                                break;
                        }
                    }
                    Thread.Sleep(50);
                    timeOutMS -= 50;
                    if (timeOutMS <= 0)
                        break;
                }
                game.Move();
            }
            Console.ReadKey();
        }

        private static void ConsoleRenderer(Game game)
        {
            Console.CursorVisible = false;
            Console.SetCursorPosition(0, 0);
            foreach (var line in textMap(game))
                Console.WriteLine(line);
        }

        private static string[] textMap(Game game)
        {
            var result = new string[game.MapHeight+2];
            var strBorder = new StringBuilder();
            strBorder.Append(' ');
            for (var i = 0; i < game.MapWidth; i++)
                strBorder.Append('-');
            strBorder.Append(' ');
            result[0] = strBorder.ToString();
            result[game.MapHeight + 1] = result[0];
            for (var j = 0; j < game.MapHeight; j++)
            {
                var str = new StringBuilder();
                str.Append('|');
                for (var i = 0; i < game.MapWidth; i++)
                {
                    switch (game.Map[i, j])
                    {
                        case Game.State.Empty:
                            str.Append(" ");
                            break;
                        case Game.State.Snake:
                            str.Append("X");
                            break;
                        case Game.State.Fruit:
                            str.Append("O");
                            break;
                    }
                }
                str.Append('|');
                if (j == 0) str.Append(" Game speed: " + game.GameSpeed);
                else if (j == 1) str.Append(" Score: " + game.Score);
                result[j + 1] = str.ToString();
            }
            return result;
        }
    }
}
