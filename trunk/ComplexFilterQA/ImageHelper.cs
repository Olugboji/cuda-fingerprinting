﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;

namespace ComplexFilterQA
{
    public static class ImageHelper
    {
        public static void MarkMinutiae(string sourcePath, List<Minutia> minutiae, string path)
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

        public static void MarkMinutiae(string sourcePath, List<Minutia> minutiae, List<Minutia> minutiae2, string path)
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

        public static double[,] LoadImage(string path)
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
            return imgBytes;
        }

        public static void SaveComplexArrayAsHSV(Complex[,] data, string path)
        {
            int X = data.GetLength(0);
            int Y = data.GetLength(1);
            var bmp = new Bitmap(X, Y);
            for (int x = 0; x < X; x++)
            {
                for (int y = 0; y < Y; y++)
                {
                    var HV = data[x, y];
                    var V = Math.Round(HV.Magnitude * 100);
                    var H = (int)(HV.Phase * 180 / Math.PI);
                    if (H < 0) H += 360;
                    var hi = H / 60;
                    var a = V * (H % 60) / 60.0d;
                    var vInc = (int)(a * 2.55d);
                    var vDec = (int)((V - a) * 2.55d);
                    var v = (int)(V * 2.55d);
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
                    bmp.SetPixel(x, y, c);
                }
            }

            bmp.Save(path, ImageFormat.Png);
        }

        public static void SaveImageAsBinary(string pathFrom, string pathTo)
        {
            var bmp = new Bitmap(pathFrom);
            using (var fs = new FileStream(pathTo, FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(bmp.Width);
                    bw.Write(bmp.Height);
                    for (int row = 0; row < bmp.Height; row++)
                    {
                        for (int column = 0; column < bmp.Width; column++)
                        {
                            var value = (int)bmp.GetPixel(column, row).R;
                            bw.Write(value);
                        }
                    }
                }
            }

        }

        public static void SaveArray(double[,] data, string path)
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
            data.Select2D((value, row, column) =>
            {
                var gray = (int)((value - min) / (max - min) * 255);
                bmp.SetPixel(row, column, Color.FromArgb(gray, gray, gray));
                return value;
            });

            bmp.Save(path);
        }

        private static void SaveBinaryAsImage(string pathFrom, string pathTo, bool applyNormalization = false)
        {
            using (var fs = new FileStream(pathFrom, FileMode.Open, FileAccess.Read))
            {
                using (var bw = new BinaryReader(fs))
                {
                    var width = bw.ReadInt32();
                    var height = bw.ReadInt32();
                    var bmp = new Bitmap(width, height);
                    if (!applyNormalization)
                    {
                        for (int row = 0; row < bmp.Height; row++)
                        {
                            for (int column = 0; column < bmp.Width; column++)
                            {
                                var value = bw.ReadInt32();
                                var c = Color.FromArgb(value, value, value);
                                bmp.SetPixel(column, row, c);
                            }
                        }
                    }
                    else
                    {
                        var arr = new float[height, width];
                        float min = float.MaxValue;
                        float max = float.MinValue;
                        for (int row = 0; row < bmp.Height; row++)
                        {
                            for (int column = 0; column < bmp.Width; column++)
                            {
                                float result = bw.ReadSingle();
                                arr[row, column] = result;
                                if (result < min) min = result;
                                if (result > max) max = result;
                            }
                        }
                        for (int row = 0; row < bmp.Height; row++)
                        {
                            for (int column = 0; column < bmp.Width; column++)
                            {
                                var value = arr[row, column];
                                int c = (int)((value - min) / (max - min) * 255);
                                Color color = Color.FromArgb(c, c, c);
                                bmp.SetPixel(column, row, color);
                            }
                        }
                    }
                    bmp.Save(pathTo, ImageFormat.Png);
                }
            }
        }
    }
}
