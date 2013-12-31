﻿using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using CUDAFingerprinting.Common;
using CUDAFingerprinting.Common.OrientationField;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obedience.Processing;

namespace Obedience.Tests
{
    [TestClass]
    public class ProcessingTests
    {
        [TestMethod]
        public void TestSegmentation()
        {
            for (int i = 0; i < 20; i++)
            {
                var fp = new FingerprintProcessor();

                int[,] mask;

                var image = ImageHelper.LoadImage(Resources.SampleFinger1);

                int rows = image.GetLength(0);
                int columns = image.GetLength(1);

                var src = image.Select2D(x => (float) x).Make1D();

                Stopwatch sw = new Stopwatch();
                sw.Start();

                var result = fp.SegmentImage(src, rows, columns, out mask, true);

                sw.Stop();
                Trace.WriteLine("Segmentation with GPU took " + sw.ElapsedMilliseconds + " ms");

                var path = Constants.Path + Guid.NewGuid() + ".png";

                ImageHelper.SaveArray(result.Make2D(rows,columns).Select2D(x=>(double)x), path);

                // should result in the cropped fp
                Process.Start(path);
            }
        }

        [TestMethod]
        public void TestSegmentationPlusBigunPlusGlobalBinarization()
        {
            for (int i = 0; i < 20; i++)
            {
                var fp = new FingerprintProcessor();

                int[,] mask;

                var image = ImageHelper.LoadImage(Resources.SampleFinger1);

                int rows = image.GetLength(0);
                int columns = image.GetLength(1);

                var src = image.Select2D(x => (float) x).Make1D();

                var result = fp.SegmentImage(src, rows, columns, out mask, true);

                Stopwatch sw = new Stopwatch();
                sw.Start();
                FingerprintProcessor.Enhance(result, columns, rows);
                result = fp.BinarizeImage(result, rows, columns, true);

                sw.Stop();
                Trace.WriteLine("Binarization with GPU took " + sw.ElapsedMilliseconds + " ms");

                var path = Constants.Path + Guid.NewGuid() + ".png";

                ImageHelper.SaveArray(result.Make2D(rows, columns).Select2D(x => (double) x), path);

                Process.Start(path);
            }
        }

        [TestMethod]
        public void TestThinning()
        {
            for (int i = 0; i < 20; i++)
            {
                var fp = new FingerprintProcessor();

                int[,] mask;

                var image = ImageHelper.LoadImage(Resources.SampleFinger1);

                int rows = image.GetLength(0);
                int columns = image.GetLength(1);

                var src = image.Select2D(x => (float)x).Make1D();

                Stopwatch sw = new Stopwatch();
                sw.Start();

                var result = fp.SegmentImage(src, rows, columns, out mask, true);

                result = fp.BinarizeImage(result, rows, columns, true);

                result = fp.ThinImage(result, rows, columns, true);

                sw.Stop();
                Trace.WriteLine("Binarization with GPU took " + sw.ElapsedMilliseconds + " ms");

                var path = Constants.Path + Guid.NewGuid() + ".png";

                ImageHelper.SaveArray(result.Make2D(rows, columns).Select2D(x => (double)x), path);

                Process.Start(path);
            }
        }

        [TestMethod]
        public void TestOrField()
        {
            for (int i = 0; i < 20; i++)
            {
                var fp = new FingerprintProcessor();

                var image = ImageHelper.LoadImage(Resources.SampleFinger1);


                int rows = image.GetLength(0);
                int columns = image.GetLength(1);

                var src = image.Select2D(x => (float) x).Make1D();

                Stopwatch sw = new Stopwatch();
                sw.Start();

                int regionSize = 17;
                int overlap = 1;

                int[,] mask;

                fp.SegmentImage(src, rows, columns, out mask, true);

                FingerprintProcessor.Enhance(src, columns, rows);

                var result = fp.MakeOrientationField(src, rows, columns, regionSize, overlap, true);

                sw.Stop();

                Trace.WriteLine("OrField with GPU took " + sw.ElapsedMilliseconds + " ms");

                var path = Constants.Path + Guid.NewGuid() + ".png";

                int orFieldWidth = columns/(regionSize - overlap);
                int orFieldHeight = rows/(regionSize - overlap);

                ImageHelper.SaveFieldAbove(image, result.Make2D(orFieldHeight, orFieldWidth).Select2D(x => (double) x),
                                           regionSize, overlap, path);

                Process.Start(path);
            }
        }

        [TestMethod]
        public void TestMinutiaExtraction()
        {
            for (int i = 0; i < 20; i++)
            {
                var fp = new FingerprintProcessor();

                int[,] mask;

                var image = ImageHelper.LoadImage(Resources.SampleFinger1);

                int rows = image.GetLength(0);
                int columns = image.GetLength(1);
                Stopwatch sw = new Stopwatch();
                sw.Start();

                var src = image.Select2D(x => (float)x).Make1D();

                int regionSize = 17;
                int overlap = 1;

                var result = fp.SegmentImage(src, rows, columns, out mask, true);

                FingerprintProcessor.Enhance(result, columns, rows);

                var orField = fp.MakeOrientationField(result, rows, columns, regionSize, overlap, true);

                int orFieldWidth = columns / (regionSize - overlap);
                int orFieldHeight = rows / (regionSize - overlap);

                result = fp.BinarizeImage(result, rows, columns, true);

                result = fp.ThinImage(result, rows, columns, true);

                List<Minutia> mins = fp.FindMinutiae(result, rows, columns, orField, orFieldHeight, orFieldWidth, mask);
                
                sw.Stop();
                Trace.WriteLine("Binarization with GPU took " + sw.ElapsedMilliseconds + " ms");

                var path = Constants.Path + Guid.NewGuid() + ".png";

                ImageHelper.MarkMinutiaeWithDirections(Resources.SampleFinger1, mins, path);

                Process.Start(path);
            }

           
        }

        

        //[TestMethod]
        //public void TestFullCycleUpToMinutiae()
        //{
        //    var fp = new FingerprintProcessor();

        //    int[,] mask;

        //    var segment = fp.SegmentImage(ImageHelper.LoadImage(Resources.SampleFinger1), out mask);
        //    Stopwatch sw = new Stopwatch();
            
        //    var result = fp.BinarizeImage(segment);
        //    sw.Start();
        //    result = fp.ThinImage(result, true);

        //    //var minutiae = fp.FindMinutiae(result);

        //    //minutiae = fp.FilterMinutiae(minutiae, mask);
        //    sw.Stop();
        //    var path = Constants.Path + Guid.NewGuid() + ".png";
        //    ImageHelper.SaveArray(result, path);
        //    //ImageHelper.MarkMinutiae(Resources.SampleFinger1, minutiae, path);

        //    Process.Start(path);
        //}

        [TestMethod]
        public void TestArrayTransformations()
        {
            var path = Constants.Path + Guid.NewGuid() + ".png";
            ImageHelper.SaveArrayAndOpen(ImageHelper.LoadImage(Resources.SampleFinger1).Make1D().Make2D(Resources.SampleFinger1.Height, Resources.SampleFinger1.Width), path);
        }

        [TestMethod]
        public void ShowImage()
        {
            var path = Constants.Path + Guid.NewGuid() + ".png";
            ImageHelper.SaveBinaryAsImage("C:\\temp\\field.bin", path, true);
            Process.Start(path);
        }

        [TestMethod]
        public void SaveImage()
        {
            ImageHelper.SaveImageAsBinaryFloat("C:\\Temp\\enh\\1_1.png", "C:\\temp\\binarized.bin");
        }

        private int delta = 50;

        private void DrawLine(int x1, int y1, int x2, int y2, int x0, int y0, int[,] arr)
        {
             int deltaX = Math.Abs(x2 - x1);
             int deltaY = Math.Abs(y2 - y1);
             int signX = x1 < x2 ? 1 : -1;
             int signY = y1 < y2 ? 1 : -1;
            //
            int error = deltaX - deltaY;
            //
            if (x0 + x2 >= 0 && x0 + x2 < arr.GetLength(0) && y0 + y2 >= 0 && y0 + y2 < arr.GetLength(1))
                arr[x0+x2, y0+y2] = 1;
            while (x1 != x2 || y1 != y2)
            {
                if (x0 + x1 >= 0 && x0 + x1 < arr.GetLength(0) && y0 + y1 >= 0 && y0 + y1 < arr.GetLength(1))
                    arr[x0 + x1, y0 + y1] = 1;
                int error2 = error * 2;
                //
                if (error2 > -deltaY)
                {
                    error -= deltaY;
                    x1 += signX;
                }
                if (error2 < deltaX)
                {
                    error += deltaX;
                    y1 += signY;
                }
            }

        }

        public void DetermineNewPoint(int x1, int y1, int x2, int y2, int x3, int y3, out int x0, out int y0)
        {
            double a1, b1, c1;
            DetermineLine(x1,y1,x2,y2,out a1, out b1, out c1);
            double a2, b2, c2;
            DetermineLine(x2, y2, x3, y3, out a2, out b2, out c2);

            double c11 = c1 + Math.Sqrt(a1 * a1 + b1 * b1) * delta;
            double c12 = c1 - Math.Sqrt(a1 * a1 + b1 * b1) * delta;
            double c21 = c2 + Math.Sqrt(a2 * a2 + b2 * b2) * delta;
            double c22 = c2 - Math.Sqrt(a2 * a2 + b2 * b2) * delta;

            double x, y;
            IntersectionPoint(a1, b1, c11, a2, b2, c21, out x, out y);
            if (!OnTheSameSide(x1, y1, x, y, a2, b2, c2) && !OnTheSameSide(x3, y3, x, y, a1, b1, c1))
            {
                x0 = (int)x;
                y0 = (int) y;
                return;
            }

            IntersectionPoint(a1, b1, c12, a2, b2, c21, out x, out y);
            if (!OnTheSameSide(x1, y1, x, y, a2, b2, c2) && !OnTheSameSide(x3, y3, x, y, a1, b1, c1))
            {
                x0 = (int)x;
                y0 = (int)y;
                return;
            }

            IntersectionPoint(a1, b1, c11, a2, b2, c22, out x, out y);
            if (!OnTheSameSide(x1, y1, x, y, a2, b2, c2) && !OnTheSameSide(x3, y3, x, y, a1, b1, c1))
            {
                x0 = (int)x;
                y0 = (int)y;
                return;
            }

            IntersectionPoint(a1, b1, c12, a2, b2, c22, out x, out y);
            x0 = (int)x;
            y0 = (int)y;
        }

        private bool OnTheSameSide(double x1, double y1, double x2, double y2, 
            double a, double b, double c)
        {
            return Math.Sign(a*x1 + b*y1 + c)*Math.Sign(a*x2 + b*y2 + c) > 0;
        }

        private void IntersectionPoint(double a1, double b1, double c1,
                                       double a2, double b2, double c2, out double x, out double y)
        {
            double det = a1*b2 - a2*b1;
            x = (-b2*c1 + b1*c2)/det;
            y = (-a1*c2 + a2*c1)/det;
        }

        private void DetermineLine(int x1, int y1, int x2, int y2, out double a1, out double b1, out double c1)
        {
            // determine line coefficients
            a1 = y1 - y2;
            b1 = x2 - x1;
            c1 = x1 * y2 - x2 * y1;

            if (a1 != 0)
            {
                c1 /= a1;
                b1 /= a1;
                a1 = 1.0d;
            }
            else
            {
                c1 /= b1;
                a1 /= b1;
                b1 = 1.0d;
            }
        }

        [TestMethod]
        public void TestFieldFilling()
        {
            int[,] result = new int[100 + 8*delta,100 + 8*delta];

            List<int> xs = new List<int>() {30, 30, 60};
            List<int> ys = new List<int>() {30, 60, 30};


            List<int> xResults = new List<int>();
            List<int> yResults = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                int x;
                int y;
                DetermineNewPoint(xs[(i + 2) % 3], ys[(i + 2) % 3], xs[i], ys[i], xs[(i + 1) % 3],
                                  ys[(i + 1)%3], out x, out y);
                xResults.Add(x);
                yResults.Add(y);
            }
            for (int i = 0; i < 3; i++)
            {
                DrawLine(xResults[i], yResults[i], xResults[(i + 1) % 3], yResults[(i + 1) % 3], delta, delta, result);
                DrawLine(xs[i], ys[i], xs[(i + 1) % 3], ys[(i + 1) % 3], delta, delta, result);
            }

            for (int x = -30; x < 200; x++)
            {
                for (int y = -30; y < 200; y++)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        double a, b, c;
                        DetermineLine(xResults[i], yResults[i], xResults[(i + 1)%3], yResults[(i + 1)%3], out a, out b,
                                      out c);
                        if (!OnTheSameSide(x, y, xResults[(i + 2)%3], yResults[(i + 2)%3], a, b, c))
                        {
                            result[x + delta, y + delta] = 1;
                            break;
                        }
                    }
                }
            }
        
            var path = "C:\\temp\\LineTest.png";
            ImageHelper.SaveArray(result.Select2D(x => (double) x), path);
            Process.Start(path);
        }
    }
}
