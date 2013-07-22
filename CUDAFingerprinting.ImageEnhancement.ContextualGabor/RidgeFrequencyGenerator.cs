﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CUDAFingerprinting.ImageEnhancement.ContextualGabor
{
    public static class RidgeFrequencyGenerator
    {
        public const int W = 16;
        public const int L = 32;

        public static double[,,] GenerateXSignature(int[,] normalizedImage)
        {
            var lro = OrientationFieldGenerator.GenerateLocalRidgeOrientation(normalizedImage);
            int maxY = lro.GetLength(0);
            int maxX = lro.GetLength(1);
            var result = new double[maxY, maxX, L];

            for (int i = 0; i < maxY; i++)
            {
                for (int j = 0; j < maxX; j++)
                {
                    for (int k = 0; k < L; k++)
                    {
                        result[i, j, k] = 0;
                        for (int d = 0; d < W; d++)
                        {
                            int u = Convert.ToInt32(i + (d - W / 2) * Math.Cos(lro[i, j]) + (k - L / 2) * Math.Sin(lro[i, j]));
                            int v = Convert.ToInt32(j + (d - W / 2) * Math.Cos(lro[i, j]) + (L / 2 - k) * Math.Sin(lro[i, j]));
                            if (u >= 0 && u < maxY && v >= 0 && v < maxX)
                                result[i, j, k] += normalizedImage[u, v];
                            else
                                result[i, j, k] = -1;
                        }
                        if (result[i, j, k] != -1)
                            result[i, j, k] /= (double)W;
                    }
                }
            }
            return result;
        }

        public static double AverageDistanceBetweenLocalMax(double[,,] XSign, int y, int x) 
        {
            int size = XSign.GetLength(2);
            var localMaxFlags = new bool[size];
            const int mask = 1;
            for (int i = 0; i < size; i++)
            {
                localMaxFlags[i] = true;    
                for (int j = i - mask; j < i + mask; j++)
			    {
                    if (j >= 0 && j < size && XSign[y, x, j] >= XSign[y, x, i] && j != i)
                        localMaxFlags[i] = false;
			    }
            }
            double counter = 0;
            double accum = 0;
            int curMax = -1;

            for (int i = 0; i < size; i++)
            {
                if (localMaxFlags[i])
                    if (curMax == -1)
                    {
                        curMax = i;
                    }
                    else
                    {
                        accum = accum + i - curMax - 1;
                        ++counter;
                        curMax = i;
                    }
            }
            if (counter == 0)
                return 0;
            return accum / counter;
        }

        public static double AverageDistanceBetweenLocalMin(double[, ,] XSign, int y, int x)
        {
            int size = XSign.GetLength(2);
            var localMaxFlags = new bool[size];
            const int mask = 1;
            for (int i = 0; i < size; i++)
            {
                localMaxFlags[i] = true;
                for (int j = i - mask; j < i + mask; j++)
                {
                    if (j >= 0 && j < size && XSign[y, x, j] <= XSign[y, x, i] && j != i)
                        localMaxFlags[i] = false;
                }
            }
            double counter = 0;
            double accum = 0;
            int curMax = -1;

            for (int i = 0; i < size; i++)
            {
                if (localMaxFlags[i])
                    if (curMax == -1)
                    {
                        curMax = i;
                    }
                    else
                    {
                        accum = accum + i - curMax - 1;
                        ++counter;
                        curMax = i;
                    }
            }
            if (counter == 0)
                return 0;
            return accum / counter;
        }

        public static double[,] GenerateFrequency(int[,] image)
        {
            var xSign = GenerateXSignature(image);
            int maxY = xSign.GetLength(0);
            int maxX = xSign.GetLength(1);
            var freq = new double[maxY, maxX];

            for (int i = 0; i < maxY; i++)
            {
                for (int j = 0; j < maxX; j++)
                {
                    double denominator = AverageDistanceBetweenLocalMax(xSign, i, j);
                    if (denominator != 0)
                        freq[i, j] = 1 / AverageDistanceBetweenLocalMax(xSign, i, j);
                    else
                        freq[i, j] = -1;
                }
            }
            return freq;
        }

        public static double[,] GenerateInterpolatedFrequency(int[,] image)
        {
            var freq = GenerateFrequency(image);
            int maxY = freq.GetLength(0);
            int maxX = freq.GetLength(1);
            const int variance = 9;
            var kernel = OrientationFieldGenerator.GenerateGaussianKernel(Math.Sqrt(variance));
            int size = kernel.GetLength(0);
            bool flag = true;
            Func<double, double> m = x => (x <= 0 ? 0 : x);
            Func<double, double> b = x => (x <= 0 ? 0 : 1);
                
            while (flag)
            {

            for (int i = 0; i < maxY; i++)
            {
                for (int j = 0; j < maxX; j++)
                {
                    if (freq[i, j] == -1)
                    {
                        // Interpolation
                        double numerator = 0;
                        double denominator = 0;

                        for (int u = -size / 2; u <= size / 2; u++)
                        {
                            for (int v = -size / 2; v <= size / 2; v++)
                            {   
                                int y = i - u;
                                int x = j - v;
                                if (x >= 0 && x < maxX && y >= 0 && y < maxY)
                                {
                                    numerator += kernel[u + size / 2, v + size / 2] * m(freq[y, x]);
                                    denominator += kernel[u + size / 2, v + size / 2] * b(freq[y, x] + 1);
                                }
                            }
                        }
                        freq[i, j] = numerator / denominator;
                    }
                }
            }


                // Check if there exist any -1 frequency
                flag = false;
                for (int i = 0; i < maxY; i++)
                {
                    for (int j = 0; j < maxX; j++)
                    {
                        if (freq[i, j] == -1)
                            flag = true;
                    }
                }
            }




            var result = new double[maxY, maxX];
            kernel = OrientationFieldGenerator.GenerateGaussianKernel(1);
            size = kernel.GetLength(0);

            for (int i = 0; i < maxY; i++)
            {
                for (int j = 0; j < maxX; j++)
                {
                    result[i, j] = 0;
                    for (int dy = -size / 2; dy <= size / 2; dy++)
                    {
                        for (int dx = -size / 2; dx <= size / 2; dx++)
                        {
                            int x = j + dx;
                            int y = i + dy;
                            if (x >= 0 && x < maxX && y >= 0 && y < maxY)
                                result[i, j] += freq[y, x] * kernel[dy + size / 2, dx + size / 2];
                        }
                    }
                }
            }


            return result;
        }

        public static List<ClustersGenerator.ClusterPoint> GenerateBlocksInfo(int[,] image)
        {
            var xSign = GenerateXSignature(image);
            var freq = GenerateInterpolatedFrequency(image);
            var result = new List<ClustersGenerator.ClusterPoint>();
            int maxY = freq.GetLength(0);
            int maxX = freq.GetLength(1);

            for (int i = 0; i < maxY; i++)
            {
                for (int j = 0; j < maxX; j++)
                {
                    double a = AverageDistanceBetweenLocalMax(xSign, i, j) - AverageDistanceBetweenLocalMin(xSign, i, j);
                    double f = 1 / freq[i, j];
                    double variance = 0;
                    double mean = 0;
                    for (int k = 0; k < L; k++)
                    {
                        mean += xSign[i, j, k];
                    }
                    mean /= L;
                    for (int k = 0; k < L; k++)
                    {
                        variance = (xSign[i, j, k] - mean) * (xSign[i, j, k] - mean);
                    }
                    variance /= L;
                    result.Add(new ClustersGenerator.ClusterPoint(a, f, variance));
                }
            }
            return result;
        }
    }
}
