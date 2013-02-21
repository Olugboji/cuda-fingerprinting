﻿using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ComplexFilterQA
{
    internal class Program
    {
        private static double sigma1 = 0.9;
        private static double sigma2 = 1.5;
        private static double sigma3 = 3.2;
        private static double K0 = 1.7;
        private static double K = 1.3;
        static int blockSize = 9;
        static double tau1 = 0.35;
        static double tau2 = 0.45;
        static double tauLS = 0.7;
        static double tauPS = 0.1;
        static int annulusSize = 2;
        static int ringInnerRadius = 2;
        static int ringOuterRadius = 4;
        static int MaxMinutiaeCount = 45;

        private static Point[,] directions = new Point[11,20];

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
            //SaveImageAsBinary("C:\\temp\\104_6.tif","C:\\temp\\104_6.bin");
            SaveBinaryAsImage("C:\\temp\\104_6_e1.bin", "C:\\temp\\104_6_e1.png");
            PreprocessFingerprint("C:\\temp\\104_6.tif");
            var minutiae1 = MinutiaeMatcher.LoadMinutiae("C:\\temp\\06.02.2013\\Minutiae_104_6.bin");

            var minutiae2 = MinutiaeMatcher.LoadMinutiae("C:\\temp\\06.02.2013\\Minutiae_104_3.bin");

            var correlation = MinutiaeMatcher.GetBestFoundMinutiaCorrelation(minutiae1, minutiae2);
            var rotation = MinutiaeMatcher.CalculateRotation(minutiae1, minutiae2, correlation.First(), correlation.Skip(1).ToList());

            var score = MinutiaeMatcher.TranslateAndMatch(minutiae1, minutiae1[correlation.First().X], minutiae2, minutiae2[correlation.First().Y], rotation);

            var translatedMinutiae = MinutiaeMatcher.TranslateToSecond(minutiae1, minutiae1[correlation.First().X], minutiae2[correlation.First().Y],
                rotation);

            MarkMinutiae("C:\\temp\\104_6.tif", minutiae2, translatedMinutiae, "C:\\temp\\15.02.2013\\Marked_with_translation_104_6.png");
        }

        private static void SaveBinaryAsImage(string pathFrom, string pathTo)
        {
            using (var fs = new FileStream(pathFrom, FileMode.Open, FileAccess.Read))
            {
                using (var bw = new BinaryReader(fs))
                {
                    var width = bw.ReadInt32();
                    var height = bw.ReadInt32();
                    var bmp = new Bitmap(width, height);
                    for (int row = 0; row < bmp.Height; row++)
                    {
                        for (int column = 0; column < bmp.Width; column++)
                        {
                            var value = bw.ReadInt32();
                            var c = Color.FromArgb(value, value, value);
                            bmp.SetPixel(column,row,c);
                        }
                    }
                    bmp.Save(pathTo,ImageFormat.Png);
                }
            }
        }

        private static void FillDirections()
        {
            for (int n = 0; n < 10; n++)
            {
                var angle = Math.PI * n / 20;

                directions[5, n] = new Point(0, 0);
                var tan = Math.Tan(angle);
                if (angle <= Math.PI / 4)
                {
                    for (int x = 1; x <= 5; x++)
                    {
                        var y = (int)Math.Round(tan * x);
                        directions[5 + x, n] = new Point(x, y);
                        directions[5 - x, n] = new Point(-x, -y);
                    }
                }
                else
                {
                    for (int y = 1; y <= 5; y++)
                    {
                        var x = (int)Math.Round((double)y/tan);
                        directions[5 + y, n] = new Point(x, y);
                        directions[5 - y, n] = new Point(-x, -y);
                    }
                }
            }
            for (int n = 10; n < 20; n++)
            {
                for (int i = 0; i < 11; i++)
                {
                    var p = directions[i, n - 10];
                    directions[i, n] = new Point(p.Y,-p.X);
                }
            }
        }

        /*private static void DrawNeatRectangles()
        {
            //this set of images illustrates the quantifization of images to 20 different directions
            int N = 20;
            double delta = Math.PI / N;
            double halfDelta = delta / 2;
            int cellSize = 50;
            int cellAmount = 11;
            int center = cellAmount / 2;
            for (int i = 0; i < N; i++)
            {
                double baselineAngle = delta * i;
                double lowerBound = baselineAngle - halfDelta;
                double upperBound = baselineAngle + halfDelta;

                var bmp = new Bitmap(cellAmount * cellSize, cellAmount * cellSize);
                var gfx = Graphics.FromImage(bmp);

                for (int x = -center; x <= center; x++)
                {
                    for (int y = -center; y <= center; y++)
                    {
                        Brush b = Brushes.White;
                        gfx.FillRectangle(b, (center + x) * cellSize, (center + y) * cellSize, cellSize, cellSize);
                        gfx.DrawRectangle(Pens.Black, (center + x) * cellSize, (center + y) * cellSize, cellSize, cellSize);
                    }
                }
                for (int j = 0; j < cellAmount; j++)
                {
                    var p = directions[j, i];
                    gfx.FillRectangle(Brushes.Red, (center + p.X) * cellSize, (center - p.Y) * cellSize, cellSize, cellSize);
                    gfx.DrawRectangle(Pens.Black, (center + p.X) * cellSize, (center - p.Y) * cellSize, cellSize, cellSize);
                }
                gfx.Save();
                bmp.Save("C:\\temp\\line" + i + ".png", ImageFormat.Png);
            }
        }*/

        private static List<Minutia> PreprocessFingerprint(string path)
        {
            var bmp = new Bitmap(path);
            double[,] imgBytes = new double[bmp.Width, bmp.Height];
            for (int x = 0; x < bmp.Width; x++)
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    imgBytes[x, y] = bmp.GetPixel(x, y).R;
                }
            }

            var g1 = Reduce2(imgBytes, 1.7d);
            var g2 = Reduce2(g1, 1.21d);
            var g3 = Reduce2(g2, K);
            var g4 = Reduce2(g3, K);
            SaveArray(g4, "C:\\temp\\104_6_r4_cpu.png");

            var p3 = Expand2(g4, K, new Size(g3.GetLength(0), g3.GetLength(1)));
            var p2 = Expand2(g3, K, new Size(g2.GetLength(0), g2.GetLength(1)));
            var p1 = Expand2(g2, 1.21d, new Size(g1.GetLength(0), g1.GetLength(1)));

            var l3 = ContrastEnhancement(KernelHelper.Subtract(g3, p3));
            var l2 = ContrastEnhancement(KernelHelper.Subtract(g2, p2));
            var l1 = ContrastEnhancement(KernelHelper.Subtract(g1, p1));


            var ls1 = EstimateLS(l1, sigma1, sigma2);
            var ls2 = EstimateLS(l2, sigma1, sigma3);
            var ls3 = EstimateLS(l3, sigma1, sigma3);

            // to hell with this attunement. 2 sets of constants is better

            var ls2Scaled =
                KernelHelper.MakeComplexFromDouble(
                    Expand2(KernelHelper.GetRealPart(ls2), K, new Size(l1.GetLength(0), l1.GetLength(1))),
                    Expand2(KernelHelper.GetImaginaryPart(ls2), K, new Size(l1.GetLength(0), l1.GetLength(1))));

            var multiplier = KernelHelper.Subtract(KernelHelper.GetPhase(ls1), KernelHelper.GetPhase(ls2Scaled));

            double d = double.NegativeInfinity;
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

            var enhanced = ll0;
            var lsEnhanced = EstimateLS(enhanced, sigma1, sigma2);
            var psEnhanced = EstimatePS(enhanced, 0.9, 2.5);

            var psi = KernelHelper.Zip2D(NormalizeArray(KernelHelper.GetMagnitude(psEnhanced)),
                KernelHelper.GetMagnitude(lsEnhanced), (x, y) => x * (1.0d - y));
            var minutiae = SearchMinutiae(psi, lsEnhanced, psEnhanced);
            return minutiae;
            /*SaveArray(KernelHelper.GetMagnitude(psEnhanced), "C:\\temp\\psEnhanced.png");
            SaveArray(psi, "C:\\temp\\psi.png");
            SaveComplexArrayAsHSV(lsEnhanced, "C:\\temp\\lsEnhanced.png");
            SaveComplexArrayAsHSV(ls1, "C:\\temp\\ls1.png");
            SaveComplexArrayAsHSV(ls2, "C:\\temp\\ls2.png");
            SaveComplexArrayAsHSV(ls3, "C:\\temp\\ls3.png");**/
        }

        private static void MarkMinutiae(string sourcePath, List<Minutia> minutiae, string path)
        {
            var bmp = new Bitmap(sourcePath);
            var bmp2 = new Bitmap(bmp.Width, bmp.Height);
            for (int x = 0; x < bmp2.Width; x++)
            {
                for (int y = 0; y < bmp2.Height; y++)
                {
                    bmp2.SetPixel(x, y, bmp.GetPixel(x, y));
                }
            }
            var gfx = Graphics.FromImage(bmp2);

            foreach (var pt in minutiae)
            {
                gfx.DrawEllipse(Pens.Red, pt.X - 2, pt.Y - 2, 5, 5);
                gfx.FillEllipse(Brushes.Red, pt.X - 2, pt.Y - 2, 5, 5);
            }

            gfx.Save();

            bmp2.Save(path, ImageFormat.Png);

        }

        private static void MarkMinutiae(string sourcePath, List<Minutia> minutiae, List<Minutia> minutiae2, string path)
        {
            var bmp = new Bitmap(sourcePath);
            var bmp2 = new Bitmap(bmp.Width, bmp.Height);
            for (int x = 0; x < bmp2.Width; x++)
            {
                for (int y = 0; y < bmp2.Height; y++)
                {
                    bmp2.SetPixel(x, y, bmp.GetPixel(x, y));
                }
            }
            var gfx = Graphics.FromImage(bmp2);

            foreach (var pt in minutiae)
            {
                gfx.DrawEllipse(Pens.Red, pt.X - 2, pt.Y - 2, 5, 5);
                gfx.FillEllipse(Brushes.Red, pt.X - 2, pt.Y - 2, 5, 5);
            }

            foreach (var pt in minutiae2)
            {
                gfx.DrawEllipse(Pens.Blue, pt.X - 2, pt.Y - 2, 5, 5);
                gfx.FillEllipse(Brushes.Blue, pt.X - 2, pt.Y - 2, 5, 5);
            }

            gfx.Save();

            bmp2.Save(path, ImageFormat.Png);

        }

        private static List<Minutia> SearchMinutiae(double[,] psi, Complex[,] lsEnhanced, Complex[,] ps)
        {
            var size = new Size(psi.GetLength(0), psi.GetLength(1));

            for (int x = 0; x < size.Width; x++)
            {
                for (int y = 0; y < size.Height; y++)
                {
                    if (psi[x, y] < tauPS) psi[x, y] = 0;
                }
            }

            var blockDim = new Size(size.Width / blockSize, size.Height / blockSize);
            List<Minutia> minutiae = new List<Minutia>();
            for (int i =2; i < blockDim.Width-2; i++)
            {
                for (int j = 2; j < blockDim.Height-2; j++)
                {
                    var max = double.NegativeInfinity;
                    int xMax = 0;
                    int yMax = 0;
                    for (int x = 0; x < blockSize; x++)
                    {
                        for (int y = 0; y < blockSize; y++)
                        {
                            if (psi[x + i * blockSize, y + j * blockSize] > max)
                            {
                                max = psi[x + i * blockSize, y + j * blockSize];
                                xMax = i * blockSize + x;
                                yMax = j * blockSize + y;
                            }
                        }
                    }
                    if (max < tauPS) continue;
                    int count = 0;
                    double sum = 0;
                    for (int dx = -ringOuterRadius; dx <= ringOuterRadius; dx++)
                    {
                        for (int dy = -ringOuterRadius; dy <= ringOuterRadius; dy++)
                        {
                            if (Math.Abs(dx) < ringInnerRadius && Math.Abs(dy) < ringInnerRadius) continue;
                            count++;
                            int xx = xMax + dx;
                            if (xx < 0) xx = 0;
                            if (xx >= size.Width) xx = size.Width - 1;
                            int yy = yMax + dy;
                            if (yy < 0) yy = 0;
                            if (yy >= size.Height) yy = size.Height - 1;
                            sum += lsEnhanced[xx, yy].Magnitude;
                        }
                    }
                    if (sum / count > tauLS)
                    {
                        if(!minutiae.Where(pt=>(pt.X-xMax)*(pt.X-xMax)+(pt.Y-yMax)*(pt.Y-yMax)<30).Any())
                            minutiae.Add(new Minutia() { X = xMax, Y = yMax, Angle = ps[xMax,yMax].Phase });
                    }
                }
            }

            var endList = minutiae.OrderByDescending(x => ps[x.X, x.Y].Magnitude)
                .Take(MaxMinutiaeCount)
                .ToList();

            return endList;
        }

        private static void DirectionFiltering(double[,] l1, Complex[,] ls, double tau1, double tau2)
        {
            var sigmaDirection = 1.6d;
            var kernel = new double[11];
            var ksum = 0d;
            for (int i = 0; i < 11; i++)
            {
                ksum+=kernel[i] = Gaussian1D(i - 5, sigmaDirection);
            }

            for (int i = 0; i < 11; i++)
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
                        for (int dx = -annulusSize; dx <= annulusSize; dx++)
                        {
                            for (int dy = -annulusSize; dy <= annulusSize; dy++)
                            {
                                int xx = x + dx;
                                if (xx < 0) xx = 0;
                                if (xx >= l1.GetLength(0)) xx = l1.GetLength(0) - 1;
                                int yy = y + dy;
                                if (yy < 0) yy = 0;
                                if (yy >= l1.GetLength(1)) yy = l1.GetLength(1) - 1;
                                sum += ls[xx, yy].Magnitude;
                            }
                        }
                        if (sum / (annulusSize * 2 + 1) * (annulusSize * 2 + 1) < tau2) l1[x, y] = 0;
                        else
                        {
                            var phase = ls[x, y].Phase/ 2 - Math.PI / 2;
                            if (phase > Math.PI*39/40) phase -= Math.PI;
                            if (phase < -Math.PI/40) phase += Math.PI;
                            var direction = (int)Math.Round(phase / (Math.PI / 20));

                            var avg = 0.0d;
                            for (int i = 0; i < 11; i++)
                            {
                                var p = directions[i, direction];
                                int xx = x + p.X;
                                if (xx < 0) xx = 0;
                                if (xx >= l1.GetLength(0)) xx = l1.GetLength(0) - 1;
                                int yy = y - p.Y;
                                if (yy < 0) yy = 0;
                                if (yy >= l1.GetLength(1)) yy = l1.GetLength(1) - 1;
                                avg += kernel[i] * l1[xx, yy];
                            }
                            l1[x, y] = avg;
                        }
                    }
                }
            }
        }

        private static Complex[,] EstimateLS(double[,] l1, double Sigma1, double Sigma2)
        {
            var kernelX = KernelHelper.MakeKernel((x, y) => Gaussian(x, y, Sigma1)*x,11);
            var resultX = ConvolutionHelper.Convolve(l1, kernelX);
            var kernelY = KernelHelper.MakeKernel((x, y) => Gaussian(x, y, Sigma1) *-y, 11);
            var resultY = ConvolutionHelper.Convolve(l1, kernelY);


            var preZ = KernelHelper.MakeComplexFromDouble(resultX, resultY);
            var z = new Complex[l1.GetLength(0),l1.GetLength(1)];
            for (int x = 0; x < l1.GetLength(0); x++)
            {
                for (int y = 0; y < l1.GetLength(1); y++)
                {
                    z[x, y] = preZ[x, y]*preZ[x, y];
                    
                }
            }

            var kernel2 = KernelHelper.MakeComplexKernel((x, y) => Gaussian(x, y, Sigma2), (x, y) => 0, 25);

            var I20 = ConvolutionHelper.ComplexConvolve(z, kernel2);

            var I11 = ConvolutionHelper.Convolve(KernelHelper.GetMagnitude(z), KernelHelper.GetRealPart(kernel2));
            Complex[,] LS = new Complex[l1.GetLength(0),l1.GetLength(1)];
            for (int x = 0; x < l1.GetLength(0); x++)
            {
                for (int y = 0; y < l1.GetLength(1); y++)
                {
                    LS[x, y] = I20[x, y]/I11[x, y];
                }
            }
            
            return LS;
        }

        private static Complex[,] EstimatePS(double[,] l1, double Sigma1, double Sigma2)
        {
            var kernelX = KernelHelper.MakeKernel((x, y) => Gaussian(x, y, Sigma1) * x, 11);
            var resultX = ConvolutionHelper.Convolve(l1, kernelX);
            var kernelY = KernelHelper.MakeKernel((x, y) => Gaussian(x, y, Sigma1) * -y, 11);
            var resultY = ConvolutionHelper.Convolve(l1, kernelY);

            var preZ = KernelHelper.MakeComplexFromDouble(resultX, resultY);
            var z = new Complex[l1.GetLength(0), l1.GetLength(1)];
            for (int x = 0; x < l1.GetLength(0); x++)
            {
                for (int y = 0; y < l1.GetLength(1); y++)
                {
                    z[x, y] = preZ[x, y] * preZ[x, y];

                }
            }

            var kernel2 = KernelHelper.MakeComplexKernel((x, y) => Gaussian(x, y, Sigma2) * x, (x,y)=>Gaussian(x, y, Sigma2) * y, 25);

            var I20 = ConvolutionHelper.ComplexConvolve(z, kernel2);

            return I20;
        }

        private static double[,] ContrastEnhancement(double[,] source)
        {
            var maxX = source.GetLength(0);
            var maxY = source.GetLength(1);
            var result = new double[maxX,maxY];
            for (int x = 0; x < maxX; x++)
            {
                for (int y = 0; y < maxY; y++)
                {
                    var d = source[x, y];
                    result[x, y] = Math.Sign(d)*Math.Sqrt(Math.Abs(d));
                }
            }

            return result;
        }

        private static double[,] Reduce2(double[,] source, double factor)
        {
            var result = new double[(int)(source.GetLength(0) / factor), (int)(source.GetLength(1) / factor)];
            Resize(source, result, factor, (x, y) => Gaussian(x, y, factor / 2d * 0.75d));
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
            for (int i = 0; i < result.GetLength(0); i++)
            {
                double x = cellSize * i;
                for (int j = 0; j < result.GetLength(1); j++)
                {
                    double y = cellSize * j;

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
                    result[i, j] = sum;
                }
            }
        }

        private static void SaveComplexArrayAsHSV(Complex[,] data, string path)
        {
            int X = data.GetLength(0);
            int Y = data.GetLength(1);
            var bmp = new Bitmap(X, Y);
            for (int x = 0; x < X; x++)
            {
                for (int y = 0; y < Y; y++)
                {
                    var HV = data[x, y];
                    var V = Math.Round(HV.Magnitude*100);
                    var H = (int)(HV.Phase*180/Math.PI);
                    if (H < 0) H += 360;
                    var hi = H/60;
                    var a = V*(H%60)/60.0d;
                    var vInc = (int) ( a*2.55d);
                    var vDec = (int) ((V - a)*2.55d);
                    var v = (int)(V*2.55d);
                    Color c;
                    switch (hi)
                    {
                        case 0:
                            c = Color.FromArgb(v, vInc, 0);
                            break;
                        case 1:
                            c = Color.FromArgb(vDec, v, 0);
                            break;
                        case 2:
                            c = Color.FromArgb(0, v, vInc);
                            break;
                        case 3:
                            c = Color.FromArgb(0, vDec, v);
                            break;
                        case 4:
                            c = Color.FromArgb(vInc, 0, v);
                            break;
                        case 5:
                            c = Color.FromArgb(v, 0, vDec);
                            break;
                        default:
                            c = Color.Black;
                            break;
                    }
                    bmp.SetPixel( x,  y, c);
                }
            }
            
            bmp.Save(path, ImageFormat.Png);
        }

        private static void SaveImageAsBinary(string pathFrom, string pathTo)
        {
            var bmp = new Bitmap(pathFrom);
            using(var fs = new FileStream(pathTo,FileMode.Create,FileAccess.Write))
            {
                using(BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(bmp.Width);
                    bw.Write(bmp.Height);
                    for (int row = 0; row < bmp.Height; row++)
                    {
                        for (int column = 0; column < bmp.Width; column++)
                        {
                            var value = (int) bmp.GetPixel(column, row).R;
                            bw.Write(value);
                        }
                    }
                }
            }
            
        }

        private static void SaveArray(double[,] data, string path)
        {
            int X = data.GetLength(0);
            int Y = data.GetLength(1);
            var max = double.NegativeInfinity;
            var min = double.PositiveInfinity;
            foreach (var num in data)
            {
                if (num > max) max = num; 
                if (num < min) min = num;
            }
            var bmp = new Bitmap(X, Y);
            for(int x=0;x<X;x++)
            {
                for (int y = 0; y < Y; y++)
                {
                    var gray = (int)((data[x, y] - min)/(max - min)*255);
                    bmp.SetPixel(x,y,Color.FromArgb(gray,gray,gray));
                }   
            }

            bmp.Save(path);
        }

        private static double[,] NormalizeArray(double[,] data)
        {
            return RearrangeArray(data, 0, 1);
        }

        private static double[,] RearrangeArray(double[,] data, double min, double max)
        {
            int X = data.GetLength(0);
            int Y = data.GetLength(1);
            var dataMax = double.NegativeInfinity;
            var dataMin = double.PositiveInfinity;
            foreach (var num in data)
            {
                if (num > dataMax) dataMax = num;
                if (num < dataMin) dataMin = num;
            }
            var result = new double[X, Y];
            for (int x = 0; x < X; x++)
            {
                for (int y = 0; y < Y; y++)
                {
                    var gray = ((data[x, y] - dataMin) / (dataMax - dataMin) * (max-min))+min;
                    result[x, y] = gray;
                }
            }

            return result;
        }

    }
}
