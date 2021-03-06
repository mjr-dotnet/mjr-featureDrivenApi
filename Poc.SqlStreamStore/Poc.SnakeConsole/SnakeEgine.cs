﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Poc.Domain.GameModule;

namespace Poc.SnakeConsole
{
    public class SnakeEgine
    {

        byte right = 0;
        byte left = 1;
        byte down = 2;
        byte up = 3;
        int lastFoodTime = 0;
        int foodDissapearTime = 8000;
        int negativePoints = 0;

        Position[] directions = {
            Position.Right, // right
            Position.Left, // left
            Position.Down, // down
            Position.Up // up
        };

        double sleepTime = 100;
        int direction;
        Random randomNumbersGenerator = new Random();
        Queue<Position> snakeElements = new Queue<Position>();
        private SqlStreamEventStore.EventStore store;
        private List<Position> obstacles;
        Position food;

        public SnakeEgine(SqlStreamEventStore.EventStore store)
        {
            this.store = store;
        }

        public void Start()
        {
            right = 0;
            left = 1;
            down = 2;
            up = 3;

            foodDissapearTime = 8000;
            negativePoints = 0;
            sleepTime = 100;
            direction = right;
            Console.BufferHeight = Console.WindowHeight;
            lastFoodTime = Environment.TickCount;

            obstacles = new List<Position>()
            {
                new Position(12, 12),
                new Position(14, 20),
                new Position(7, 7),
                new Position(19, 19),
                new Position(6, 9),
            };
            WriteObstacles(obstacles);

            snakeElements = new Queue<Position>();
            for (int i = 0; i <= 5; i++)
            {
                snakeElements.Enqueue(new Position(0, i));
            }


            do
            {
                food = new Position(randomNumbersGenerator.Next(0, Console.WindowHeight),
                    randomNumbersGenerator.Next(0, Console.WindowWidth));
            }
            while (snakeElements.Contains(food) || obstacles.Contains(food));
            WriteFood(food);

            WriteSnakeStart(snakeElements);
        }

        public async Task Play(Snake snake, bool isReplaying)
        {
            int currentPosition = 0;
            while (true)
            {
                if (Console.KeyAvailable && !isReplaying)
                {

                    ConsoleKeyInfo userInput = Console.ReadKey();
                    if (userInput.Key == ConsoleKey.LeftArrow)
                    {
                        if (direction != right) direction = left;
                    }
                    if (userInput.Key == ConsoleKey.RightArrow)
                    {
                        if (direction != left) direction = right;
                    }
                    if (userInput.Key == ConsoleKey.UpArrow)
                    {
                        if (direction != down) direction = up;
                    }
                    if (userInput.Key == ConsoleKey.DownArrow)
                    {
                        if (direction != up) direction = down;
                    }

                }

                Position snakeHead = snakeElements.Last();
                Position nextDirection = directions[direction];
                if (isReplaying)
                {
                    if (currentPosition + 1 <= snake.Positions.Count)
                        nextDirection = snake.Positions.ElementAt(currentPosition++);
                    else
                    {
                        //continue
                        Console.ReadKey();
                        isReplaying = false;
                    }
                }
                else
                {
                    if (nextDirection == Position.Left)
                        snake.Left();
                    if (nextDirection == Position.Right)
                        snake.Right();
                    if (nextDirection == Position.Up)
                        snake.Up();
                    if (nextDirection == Position.Down)
                        snake.Down();
                    await store.Save(snake, Guid.NewGuid(), (d) => { });
                }
                negativePoints++;
                Position snakeNewHead = new Position(snakeHead.row + nextDirection.row,
                    snakeHead.col + nextDirection.col);

                if (snakeNewHead.col < 0) snakeNewHead.col = Console.WindowWidth - 1;
                if (snakeNewHead.row < 0) snakeNewHead.row = Console.WindowHeight - 1;
                if (snakeNewHead.row >= Console.WindowHeight) snakeNewHead.row = 0;
                if (snakeNewHead.col >= Console.WindowWidth) snakeNewHead.col = 0;

                if (snakeElements.Contains(snakeNewHead) || obstacles.Contains(snakeNewHead))
                {
                    Console.SetCursorPosition(0, 0);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Game over!");
                    int userPoints = (snakeElements.Count - 6) * 100 - negativePoints;
                    //if (userPoints < 0) userPoints = 0;
                    userPoints = Math.Max(userPoints, 0);
                    Console.WriteLine("Your points are: {0}", userPoints);
                    return;
                }

                WriteSnake(snakeHead);

                snakeElements.Enqueue(snakeNewHead);
                Console.SetCursorPosition(snakeNewHead.col, snakeNewHead.row);
                Console.ForegroundColor = ConsoleColor.Gray;
                if (nextDirection == Position.Right) WriteRight();
                if (nextDirection == Position.Left) WriteLeft();
                if (nextDirection == Position.Up) WriteUp();
                if (nextDirection == Position.Down) WriteDown();


                if (snakeNewHead.col == food.col && snakeNewHead.row == food.row)
                {
                    // feeding the snake
                    do
                    {
                        food = new Position(randomNumbersGenerator.Next(0, Console.WindowHeight),
                            randomNumbersGenerator.Next(0, Console.WindowWidth));

                    } while (snakeElements.Contains(food) || obstacles.Contains(food));
                    lastFoodTime = Environment.TickCount;
                    WriteFood(food);
                    sleepTime--;

                    Position obstacle = new Position();
                    do
                    {
                        obstacle = new Position(randomNumbersGenerator.Next(0, Console.WindowHeight),
                            randomNumbersGenerator.Next(0, Console.WindowWidth));
                    } while (snakeElements.Contains(obstacle) ||
                             obstacles.Contains(obstacle) ||
                             (food.row != obstacle.row && food.col != obstacle.row));
                    obstacles.Add(obstacle);
                    WriteObstacle(obstacle);
                }
                else
                {
                    // moving...
                    Position last = snakeElements.Dequeue();
                    DeleteMove(last);
                }

                if (Environment.TickCount - lastFoodTime >= foodDissapearTime)
                {
                    negativePoints = negativePoints + 50;
                    DeleteFood(food);
                    do
                    {
                        food = new Position(randomNumbersGenerator.Next(0, Console.WindowHeight),
                            randomNumbersGenerator.Next(0, Console.WindowWidth));
                    } while (snakeElements.Contains(food) || obstacles.Contains(food));
                    lastFoodTime = Environment.TickCount;
                }
                WriteFood(food);

                sleepTime -= 0.01;

                Thread.Sleep((int)sleepTime);
            }
        }

        private static void WriteDown()
        {
            Console.Write("v");
        }

        private static void WriteUp()
        {
            Console.Write("^");
        }

        private static void WriteLeft()
        {
            Console.Write("<");
        }

        private static void WriteRight()
        {
            Console.Write(">");
        }

        private static void DeleteMove(Position last)
        {
            Console.SetCursorPosition(last.col, last.row);
            Console.Write(" ");
        }

        private static void DeleteFood(Position food)
        {
            Console.SetCursorPosition(food.col, food.row);
            Console.Write(" ");
        }

        private static void WriteSnakeStart(Queue<Position> snakeElements)
        {
            foreach (Position position in snakeElements)
            {
                WriteSnake(position);
            }
        }

        private static void WriteSnake(Position position)
        {
            Console.SetCursorPosition(position.col, position.row);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("*");
        }

        private static void WriteFood(Position food)
        {
            Console.SetCursorPosition(food.col, food.row);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("@");
        }



        private static void WriteObstacles(List<Position> obstacles)
        {
            foreach (Position obstacle in obstacles)
            {
                WriteObstacle(obstacle);
            }
        }

        private static void WriteObstacle(Position obstacle)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.SetCursorPosition(obstacle.col, obstacle.row);
            Console.Write("=");
        }
    }
}
