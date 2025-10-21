using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ConwayGameOfLifeWPF
{
    public class GameOfLifeEngine
    {
        private bool[,] _currentGrid;
        private bool[,] _nextGrid;
        private readonly int _width;
        private readonly int _height;
        private readonly Random _rand = new Random();

        public int GenerationCount { get; private set; } = 0;
        public int PopulationCount { get; private set; } = 0;

        public GameOfLifeEngine(int width, int height)
        {
            _width = width;
            _height = height;
            _currentGrid = new bool[width, height];
            _nextGrid = new bool[width, height];
        }

        public bool[,] GetCurrentGrid() => _currentGrid;

        public void SetCell(int x, int y, bool isAlive)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
            {
                _currentGrid[x, y] = isAlive;
                RecalculatePopulation();
            }
        }

        public void Clear()
        {
            Array.Clear(_currentGrid, 0, _currentGrid.Length);
            Array.Clear(_nextGrid, 0, _nextGrid.Length);
            GenerationCount = 0;
            PopulationCount = 0;
        }

        public void Randomize(double probability = 0.5)
        {
            GenerationCount = 0;
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    _currentGrid[x, y] = _rand.NextDouble() < probability;
                }
            }
            RecalculatePopulation();
        }

        public void LoadPattern(List<Point> pattern, int offsetX, int offsetY)
        {
            Clear();
            foreach (var p in pattern)
            {
                int x = (offsetX + p.X + _width) % _width;
                int y = (offsetY + p.Y + _height) % _height;
                _currentGrid[x, y] = true;
            }
            RecalculatePopulation();
        }

        public void Update()
        {
            int currentPopulation = 0;

            Parallel.For(0, _width, x =>
            {
                for (int y = 0; y < _height; y++)
                {
                    int liveNeighbors = CountLiveNeighbors(x, y);
                    bool cellIsAlive = _currentGrid[x, y];

                    if (cellIsAlive)
                    {
                        _nextGrid[x, y] = (liveNeighbors == 2 || liveNeighbors == 3);
                    }
                    else
                    {
                        _nextGrid[x, y] = (liveNeighbors == 3);
                    }

                    if (_nextGrid[x, y])
                    {
                        System.Threading.Interlocked.Increment(ref currentPopulation);
                    }
                }
            });

            (_currentGrid, _nextGrid) = (_nextGrid, _currentGrid);
            GenerationCount++;
            PopulationCount = currentPopulation;
        }

        private void RecalculatePopulation()
        {
            PopulationCount = 0;
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    if (_currentGrid[x, y]) PopulationCount++;
                }
            }
        }

        private int CountLiveNeighbors(int x, int y)
        {
            int count = 0;
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    int checkX = (x + i + _width) % _width;
                    int checkY = (y + j + _height) % _height;
                    if (_currentGrid[checkX, checkY]) count++;
                }
            }
            return count;
        }

        public double CalculateShannonEntropy(int blockSize)
        {
            if (PopulationCount == 0) return 0;

            Dictionary<string, int> patternCounts = new Dictionary<string, int>();
            int totalBlocks = 0;
            StringBuilder sb = new StringBuilder(blockSize * blockSize);

            for (int x = 0; x < _width; x += blockSize)
            {
                for (int y = 0; y < _height; y += blockSize)
                {
                    sb.Clear();
                    bool blockHasLife = false;
                    for (int i = 0; i < blockSize; i++)
                    {
                        for (int j = 0; j < blockSize; j++)
                        {
                            bool isAlive = _currentGrid[(x + i) % _width, (y + j) % _height];
                            sb.Append(isAlive ? '1' : '0');
                            if (isAlive) blockHasLife = true;
                        }
                    }

                    if (!blockHasLife) continue;

                    totalBlocks++;
                    string pattern = sb.ToString();
                    if (patternCounts.ContainsKey(pattern))
                    {
                        patternCounts[pattern]++;
                    }
                    else
                    {
                        patternCounts[pattern] = 1;
                    }
                }
            }

            if (totalBlocks == 0) return 0;

            double entropy = 0.0;
            foreach (var count in patternCounts.Values)
            {
                double probability = (double)count / totalBlocks;
                entropy -= probability * Math.Log(probability, 2);
            }

            return entropy;
        }

        public int CountActiveBoxes(int boxSize)
        {
            int activeBoxes = 0;
            for (int x = 0; x < _width; x += boxSize)
            {
                for (int y = 0; y < _height; y += boxSize)
                {
                    bool boxIsActive = false;
                    for (int i = 0; i < boxSize && (x + i) < _width; i++)
                    {
                        for (int j = 0; j < boxSize && (y + j) < _height; j++)
                        {
                            if (_currentGrid[x + i, y + j])
                            {
                                boxIsActive = true;
                                break;
                            }
                        }
                        if (boxIsActive) break;
                    }

                    if (boxIsActive)
                    {
                        activeBoxes++;
                    }
                }
            }
            return activeBoxes;
        }

        public string GetBinaryGridHash()
        {
            BitArray ba = new BitArray(_width * _height);
            int k = 0;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    ba.Set(k++, _currentGrid[x, y]);
                }
            }

            byte[] bytes = new byte[(_width * _height + 7) / 8 + 1];
            ba.CopyTo(bytes, 0);

            using (var sha = SHA256.Create())
            {
                var hashBytes = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }

    public struct Point
    {
        public int X;
        public int Y;
        public Point(int x, int y) { X = x; Y = y; }
    }
}