﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace ComplexFilterQA
{
    internal class Program
    {
        private static double sigma1 = 0.6;
        private static double sigma2 = 3.2;
        private static double K0 = 1.7;
        private static double K = 1.3;
        const int NeighborhoodSize = 9;
        static double tau1 = 0.1;
        static double tau2 = 0.3;
        static double tauLS = 0.9;
        static double tauPS = 0.5;
        static int ringInnerRadius = 4;
        static int ringOuterRadius = 6;
        static int MaxMinutiaeCount = 32;

        static double sigmaDirection = 2d;
        static int directionSize = KernelHelper.GetKernelSizeForGaussianSigma(sigmaDirection);

        private static Point[,] directions = new Point[directionSize, 20];

        private static double Gaussian1D(double x, double sigma)
        {
            var commonDenom = 2.0d * sigma * sigma;
            var denominator = sigma * Math.Sqrt(Math.PI * 2);
            var result = Math.Exp(-(x * x) / commonDenom) / denominator;
            return result;
        }

        private static double Gaussian(double x, double y, double sigma)
        {
            var commonDenom = 2.0d*sigma*sigma;
            var denominator = Math.PI*commonDenom;
            var result = Math.Exp(-(x*x + y*y)/commonDenom)/denominator;
            return result;
        }

        private static void Main(string[] args)
        {
            FillDirections();
            var path1 = "C:\\temp\\enh\\6_7.tif";
            var path2 = "C:\\temp\\enh\\6_6.tif";
            var path3 = "C:\\temp\\enh\\104_3.tif";
            var path4 = "C:\\temp\\enh\\108_8.tif";

            //ImageHelper.MarkMinutiae(path, ProcessFingerprint(ImageHelper.LoadImage(path)), "C:\\temp\\6_7_out.png");
            
            var minutiae1 = ProcessFingerprint(ImageHelper.LoadImage(path1));
            var minutiae2 = ProcessFingerprint(ImageHelper.LoadImage(path2));
            var minutiae3 = ProcessFingerprint(ImageHelper.LoadImage(path3));
            var minutiae4 = ProcessFingerprint(ImageHelper.LoadImage(path4));

            var score1 = MinutiaeMatcher.Match(minutiae1, minutiae2);
            var score2 = MinutiaeMatcher.Match(minutiae1, minutiae3);
            var score3 = MinutiaeMatcher.Match(minutiae1, minutiae4);
        }

        private static void FillDirections()
        {
            for (int n = 0; n < 10; n++)
            {
                var angle = Math.PI*n/20;

                directions[directionSize/2, n] = new Point(0, 0);
                var tan = Math.Tan(angle);
                if (angle <= Math.PI/4)
                {
                    for (int x = 1; x <= directionSize/2; x++)
                    {
                        var y = (int) Math.Round(tan*x);
                        directions[directionSize/2 + x, n] = new Point(x, y);
                        directions[directionSize/2 - x, n] = new Point(-x, -y);
                    }
                }
                else
                {
                    for (int y = 1; y <= directionSize/2; y++)
                    {
                        var x = (int) Math.Round((double) y/tan);
                        directions[directionSize/2 + y, n] = new Point(x, y);
                        directions[directionSize / 2 - y, n] = new Point(-x, -y);
                    }
                }
            }
            for (int n = 10; n < 20; n++)
            {
                for (int i = 0; i < directionSize; i++)
                {
                    var p = directions[i, n - 10];
                    directions[i, n] = new Point(p.Y, -p.X);
                }
            }
        }

        private static List<Minutia> ProcessFingerprint(double[,] imgBytes)
        {
            var lsEnhanced = EstimateLS(imgBytes, sigma1, sigma2);
            var psEnhanced = EstimatePS(imgBytes, sigma1, sigma2);
            //ImageHelper.SaveComplexArrayAsHSV(lsEnhanced,"C:\\temp\\lsenh.png");

            //ImageHelper.SaveArray(NormalizeArray(psEnhanced.Select2D(x=>x.Magnitude)), "C:\\temp\\psenh.png");

            var psi = KernelHelper.Zip2D(psEnhanced,
                lsEnhanced.Select2D(x=>x.Magnitude), (x, y) => x * (1.0d - y));

            return SearchMinutiae(psi, lsEnhanced, psEnhanced);
        }

        private static double[,] EnhanceImage(double[,] imgBytes)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var g1 = Reduce2(imgBytes, 1.7d);
            var g2 = Reduce2(g1, 1.21d);
            var g3 = Reduce2(g2, K);
            var g4 = Reduce2(g3, K);

            var p3 = Expand2(g4, K, new Size(g3.GetLength(0), g3.GetLength(1)));
            var p2 = Expand2(g3, K, new Size(g2.GetLength(0), g2.GetLength(1)));
            var p1 = Expand2(g2, 1.21d, new Size(g1.GetLength(0), g1.GetLength(1)));

            var l3 = ContrastEnhancement(KernelHelper.Subtract(g3, p3));
            var l2 = ContrastEnhancement(KernelHelper.Subtract(g2, p2));
            var l1 = ContrastEnhancement(KernelHelper.Subtract(g1, p1));
            //SaveArray(l3, "C:\\temp\\l3.png");
            //SaveArray(l1, "C:\\temp\\l1.png");
            //SaveArray(l2, "C:\\temp\\l2.png");

            var ls1 = EstimateLS(l1, sigma1, sigma2);
            var ls2 = EstimateLS(l2, sigma1, sigma2);
            var ls3 = EstimateLS(l3, sigma1, sigma2);

            //SaveComplexArrayAsHSV(ls1, "C:\\temp\\ls1.png");
            //SaveComplexArrayAsHSV(ls2, "C:\\temp\\ls2.png");
            //SaveComplexArrayAsHSV(ls3, "C:\\temp\\ls3.png");

            var ls2Scaled =
                KernelHelper.MakeComplexFromDouble(
                    Expand2(ls2.Select2D(x=>x.Real), K, new Size(l1.GetLength(0), l1.GetLength(1))),
                    Expand2(ls2.Select2D(x=>x.Imaginary), K, new Size(l1.GetLength(0), l1.GetLength(1))));
            var multiplier = KernelHelper.Subtract(ls1.Select2D(x => x.Phase), ls2Scaled.Select2D(x => x.Phase));

            for (int x = 0; x < ls1.GetLength(0); x++)
            {
                for (int y = 0; y < ls1.GetLength(1); y++)
                {
                    ls1[x, y] *= Math.Abs(Math.Cos(multiplier[x, y]));
                }
            }

            DirectionFiltering(l1, ls1, tau1, tau2);
            DirectionFiltering(l2, ls2, tau1, tau2);
            DirectionFiltering(l3, ls3, tau1, tau2);

            var ll2 = Expand2(l3, K, new Size(l2.GetLength(0), l2.GetLength(1)));
            l2 = KernelHelper.Add(ll2, l2);
            var ll1 = Expand2(l2, 1.21d, new Size(l1.GetLength(0), l1.GetLength(1)));
            l1 = KernelHelper.Add(ll1, l1);
            var ll0 = Expand2(l1, 1.7d, new Size(imgBytes.GetLength(0), imgBytes.GetLength(1)));

            ll0 = ContrastEnhancement(ll0);
            sw.Stop();
            var enhanced = RearrangeArray(ll0, 0, 255);
            return enhanced;
        }

        private static List<Minutia> SearchMinutiae(Complex[,] psi, Complex[,] lsEnhanced, Complex[,] ps)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var size = new Size(psi.GetLength(0), psi.GetLength(1));

            for (int x = 0; x < size.Width; x++)
            {
                for (int y = 0; y < size.Height; y++)
                {
                    if (psi[x, y].Magnitude < tauPS) psi[x, y] = 0;
                }
            }

            var maxs = psi.Select2D((x, row, column) =>
                {
                    Point maxP = new Point();
                    double maxM = 0;
                    for (int dRow = -NeighborhoodSize/2; dRow <= NeighborhoodSize/2; dRow++)
                    {
                        for (int dColumn = -NeighborhoodSize / 2; dColumn <= NeighborhoodSize / 2; dColumn++)
                        {
                            var correctRow = row + dRow < 0
                                                 ? 0
                                                 : (row + dRow >= psi.GetLength(0) ? psi.GetLength(0) - 1 : row + dRow);
                            var correctColumn = column + dColumn < 0
                                                 ? 0
                                                 : (column + dColumn >= psi.GetLength(1) ? psi.GetLength(1) - 1 : column + dColumn);
                            var value = psi[correctRow, correctColumn];
                            if (value.Magnitude > maxM)
                            {
                                maxM = value.Magnitude;
                                maxP = new Point(correctRow, correctColumn);
                            }
                        }
                    }
                    return maxP;
                });

            Dictionary<Point, int> responses = new Dictionary<Point, int>();

            foreach (var point in maxs)
            {
                if (!responses.ContainsKey(point)) responses[point] = 0;
                responses[point] ++;
            }

            var orderedListOfCandidates =
                responses.Where(x => x.Value >= 20 && x.Key.X > 0 && x.Key.Y > 0 && x.Key.X < psi.GetLength(0)-1 && x.Key.Y < psi.GetLength(1)-1)
                         .OrderByDescending(x => x.Value)
                         .Select(x => x.Key)
                         .Where(x => psi[x.X, x.Y].Magnitude >= tauPS);
            List<Minutia> minutiae = new List<Minutia>();

            foreach (var candidate in orderedListOfCandidates)
            {
                int count = 0;
                double sum = 0;
                for (int dx = -ringOuterRadius; dx <= ringOuterRadius; dx++)
                {
                    for (int dy = -ringOuterRadius; dy <= ringOuterRadius; dy++)
                    {
                        if (Math.Abs(dx) < ringInnerRadius && Math.Abs(dy) < ringInnerRadius) continue;
                        count++;
                        int xx = candidate.X + dx;
                        if (xx < 0) xx = 0;
                        if (xx >= size.Width) xx = size.Width - 1;
                        int yy = candidate.Y + dy;
                        if (yy < 0) yy = 0;
                        if (yy >= size.Height) yy = size.Height - 1;
                        sum += lsEnhanced[xx, yy].Magnitude;
                    }
                }
                if (sum / count > tauLS)
                {
                    if (!minutiae.Any(pt => (pt.X - candidate.X) * (pt.X - candidate.X) + (pt.Y - candidate.Y) * (pt.Y - candidate.Y) < 30))
                        minutiae.Add(new Minutia() { X = candidate.X, Y = candidate.Y, Angle = ps[candidate.X, candidate.Y].Phase });
                }
            }

            var endList = minutiae.OrderByDescending(x => ps[x.X, x.Y].Magnitude)
                .Take(MaxMinutiaeCount)
                .ToList();
            sw.Stop();
            return endList;
        }

        private static void DirectionFiltering(double[,] l1, Complex[,] ls, double tau1, double tau2)
        {
            var l1Copy = new double[l1.GetLength(0),l1.GetLength(1)];
            for(int x=0;x<l1.GetLength(0);x++)
            {
                for (int y = 0; y < l1.GetLength(1); y++)
                {
                    l1Copy[x, y] = l1[x, y];
                }   
            }

            var kernel = new double[directionSize];
            var ksum = 0d;
            for (int i = 0; i < directionSize; i++)
            {
                ksum += kernel[i] = Gaussian1D(i - directionSize / 2, sigmaDirection);
            }

            for (int i = 0; i < directionSize; i++)
            {
                kernel[i] /= ksum;
            }
            for (int x = 0; x < l1.GetLength(0); x++)
            {
                for (int y = 0; y < l1.GetLength(1); y++)
                {
                    if (ls[x, y].Magnitude < tau1)
                    {
                        l1[x, y] = 0;
                    }
                    else
                    {
                        double sum = 0;
                        int area = 0;
                        for (int dx = -ringOuterRadius; dx <= ringOuterRadius; dx++)
                        {
                            for (int dy = -ringOuterRadius; dy <= ringOuterRadius; dy++)
                            {
                                if (Math.Abs(dy) < ringInnerRadius || Math.Abs(dx) < ringInnerRadius) continue;
                                int xx = x + dx;
                                if (xx < 0) xx = 0;
                                if (xx >= l1.GetLength(0)) xx = l1.GetLength(0) - 1;
                                int yy = y + dy;
                                if (yy < 0) yy = 0;
                                if (yy >= l1.GetLength(1)) yy = l1.GetLength(1) - 1;
                                sum += ls[xx, yy].Magnitude;
                                area++;
                            }
                        }
                        if (sum / area < tau2) l1[x, y] = 0;
                        else
                        {
                            var phase = ls[x, y].Phase/2 - Math.PI/2;
                            if (phase > Math.PI*39/40) phase -= Math.PI;
                            if (phase < -Math.PI/40) phase += Math.PI;
                            var direction = (int) Math.Round(phase/(Math.PI/20));

                            var avg = 0.0d;
                            for (int i = 0; i < directionSize; i++)
                            {
                                var p = directions[i, direction];
                                int xx = x + p.X;
                                if (xx < 0) xx = 0;
                                if (xx >= l1.GetLength(0)) xx = l1.GetLength(0) - 1;
                                int yy = y - p.Y;
                                if (yy < 0) yy = 0;
                                if (yy >= l1.GetLength(1)) yy = l1.GetLength(1) - 1;
                                avg += kernel[i]*l1Copy[xx, yy];
                            }
                            l1[x, y] = avg;
                        }
                    }
                }
            }
        }

        private static Complex[,] EstimateLS(double[,] l1, double Sigma1, double Sigma2)
        {
            var kernelX = KernelHelper.MakeKernel((x, y) => Gaussian(x, y, Sigma1)*x,KernelHelper.GetKernelSizeForGaussianSigma(Sigma1));
            var resultX = ConvolutionHelper.Convolve(l1, kernelX);
            var kernelY = KernelHelper.MakeKernel((x, y) => Gaussian(x, y, Sigma1) * -y, KernelHelper.GetKernelSizeForGaussianSigma(Sigma1));
            var resultY = ConvolutionHelper.Convolve(l1, kernelY);


            var preZ = KernelHelper.MakeComplexFromDouble(resultX, resultY);

            var z = preZ.Select2D(x => x*x);

            var kernel2 = KernelHelper.MakeComplexKernel((x, y) => Gaussian(x, y, Sigma2), (x, y) => 0, 
                KernelHelper.GetKernelSizeForGaussianSigma(Sigma2));

            var I20 = ConvolutionHelper.ComplexConvolve(z, kernel2);

            var I11 = ConvolutionHelper.Convolve(z.Select2D(x => x.Magnitude), kernel2.Select2D(x => x.Real));

            Complex[,] LS = KernelHelper.Zip2D(I20,I11,(x,y)=>x/y);
            
            return LS;
        }

        private static Complex[,] EstimatePS(double[,] l1, double Sigma1, double Sigma2)
        {
            var kernelX = KernelHelper.MakeKernel((x, y) => Gaussian(x, y, Sigma1) * x, KernelHelper.GetKernelSizeForGaussianSigma(Sigma1));
            var resultX = ConvolutionHelper.Convolve(l1, kernelX);
            var kernelY = KernelHelper.MakeKernel((x, y) => Gaussian(x, y, Sigma1) * -y, KernelHelper.GetKernelSizeForGaussianSigma(Sigma1));
            var resultY = ConvolutionHelper.Convolve(l1, kernelY);

            var preZ = KernelHelper.MakeComplexFromDouble(resultX, resultY);

            var z = preZ.Select2D(x => x * x);

            var kernel2 = KernelHelper.MakeComplexKernel((x, y) => Gaussian(x, y, Sigma2) * x, (x, y) => Gaussian(x, y, Sigma2) * y, KernelHelper.GetKernelSizeForGaussianSigma(Sigma2));

            var I20 = ConvolutionHelper.ComplexConvolve(z, kernel2);

            return I20;
        }

        private static double[,] ContrastEnhancement(double[,] source)
        {
            return source.Select2D(x => Math.Sign(x)*Math.Sqrt(Math.Abs(x)));
        }

        private static double[,] Reduce2(double[,] source, double factor)
        {

            var smoothed = ConvolutionHelper.Convolve(source,
                                                      KernelHelper.MakeKernel(
                                                          (x, y) => Gaussian(x, y, factor / 2d * 0.75d), KernelHelper.GetKernelSizeForGaussianSigma(factor / 2d * 0.75d)));
            var result = new double[(int)(source.GetLength(0) / factor), (int)(source.GetLength(1) / factor)];
            Resize(smoothed, result, factor, (x, y) => Gaussian(x, y, factor/2d*0.75d));
            return result;
        }

        private static double[,] Expand2(double[,] source, double factor, Size requestedSize = default(Size))
        {
            double[,] result = requestedSize == default(Size)
                                   ? new double[(int)(source.GetLength(0) * factor), (int)(source.GetLength(1) * factor)]
                                   : new double[requestedSize.Width, requestedSize.Height];
            Resize(source, result, 1/factor, (x, y) => Gaussian(x, y, factor / 2d * 0.75d));
            return result;
        }

        private static void Resize(double[,] source, double[,] result, double cellSize, Func<double, double, double> filterFunction)
        {
            for (int row = 0; row < result.GetLength(0); row++)
            {
                for (int column = 0; column < result.GetLength(1); column++)
                {
                    double x = cellSize * row;
                    double y = cellSize * column;

                    double sum = 0;
                    double filterSum = 0;

                    for (int xm = (int)x - 5; xm <= (int)x + 5; xm++)
                    {
                        if (xm < 0)continue;
                        if (xm >= source.GetLength(0)) break;
                        for (int ym = (int)y - 5; ym <= (int)y + 5; ym++)
                        {
                            if (ym < 0) continue;
                            if (ym >= source.GetLength(1)) break;
                            var filterValue = filterFunction(x - xm, y - ym);
                            filterSum += filterValue;
                            sum += source[xm, ym] * filterValue;
                        }
                    }
                    sum /= filterSum;
                    result[row, column] = sum;
                }
            }
        }

        private static double[,] NormalizeArray(double[,] data)
        {
            return RearrangeArray(data, 0, 1);
        }

        private static double[,] RearrangeArray(double[,] data, double min, double max)
        {
            var dataMax = double.NegativeInfinity;
            var dataMin = double.PositiveInfinity;
            foreach (var num in data)
            {
                if (num > dataMax) dataMax = num;
                if (num < dataMin) dataMin = num;
            }
            return data.Select2D((value, row, column) => ((value - dataMin)/(dataMax - dataMin)*(max - min)) + min);
        }

    }
}
