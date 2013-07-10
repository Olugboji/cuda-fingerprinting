﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CUDAFingerprint
{
    internal class Program
    {
        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int disposeDevice();

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int initDevice();

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int createGaborFilters(float[] result, float frequency, int amount);

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void createFingerCode(int[] imgBytes, float[] result, int width, int height, int filterAmount,
                                                         int numBands, int numSectors,
                                                         int holeRadius, int bandRadius, int referencePointX,
                                                         int referencePointY);

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void fullFilterImage(int[] imgBytes, int[] result, int width, int height, 
													int filterAmount, int numBands,
                                                    int holeRadius, int bandRadius, int referencePointX,
                                                    int referencePointY);

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void normalizeImage(int[] imgBytes, int[] result, int width, int height, int numBands,
                                                  int holeRadius, int bandRadius, int referencePointX,
                                                  int referencePointY);

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void findCorePoint(int[] imgBytes, int width, int height, ref int xCore, ref int yCore);

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void matchFingercode(float[] fingercode, float numberOfRegions, float[] result);

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void loadFingerCodeDatabase(float[] fingers, int numberOfRegions, int numberOfCodes);

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void loadNoncoalescedFingerCodeDatabase(float[] fingers, int numberOfRegions, int numberOfCodes);

        [DllImport("CUDAFingercode.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void sortArrayAndIndexes(float[] arr, int[] arrIndexes, int amount);

        [DllImport("CUDAConvexHull.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void BuildHull(int[] arr, int NoM, int[] Hull,int NHull);

        [DllImport("CUDAConvexHull.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FieldFilling(bool[] field,int rows, int columns,int[] arr, int NoM);

        [DllImport("CUDAConvexHull.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void WorkingArea(bool[] field, int rows, int columns, int radius);
    private static void Main(string[] args)
        {
            // TestQuality();
            // TestTimings();
            TestSorting();
            //TestHull();
        }

        private static void TestSorting()
        {
            int N = 1000000;
            int K = 10000;
            
            Random r = new Random();

            Stopwatch sw = new Stopwatch();
            int[] indexes = new int[N];
            long max = long.MinValue;
            long min = long.MaxValue;
            long total = 0;
            Dictionary<long, int> times = new Dictionary<long, int>();
            for (int k = 0; k < K; k++)
            {
                float[] arr = new float[N];
                for (int i = 0; i < N; i++)
                {
                    arr[i] = (float)r.NextDouble() * 500.0f;
                }
                sw.Start();

                for (int i = 0; i < N; i++) indexes[i] = i;
                sortArrayAndIndexes(arr, indexes, N);
                sw.Stop();
                if (sw.ElapsedMilliseconds > max) max = sw.ElapsedMilliseconds;
                if (sw.ElapsedMilliseconds < min) min = sw.ElapsedMilliseconds;
                total += sw.ElapsedMilliseconds;
                if (!times.ContainsKey(sw.ElapsedMilliseconds)) times[sw.ElapsedMilliseconds] = 0;
                times[sw.ElapsedMilliseconds] += 1;
                sw.Reset();
            }
            Console.WriteLine("Min: {0}, Max: {1}, Average: {2}",min,max,(double) total/K);
            using(FileStream fs = new FileStream("C:\\temp\\magnitude.xls",FileMode.Create))
            {
                using(StreamWriter w = new StreamWriter(fs))
                {
                    foreach (var key in times.Keys.OrderBy(x=>x))
                    {
                        w.WriteLine("{0};{1}", key, times[key]);
                    }
                }
            }
            Console.ReadKey();
        }

        private static void TestQuality()
        {
            initDevice();
            var files = Directory.GetFiles(".\\Fingerprints");
            var images = new Dictionary<int[], int>();
            // the value is the number of finger in the database the fingerprint belogns to
            var referencePoints = new Dictionary<int[], Point>();
            var referencePointsAuto = new Dictionary<string, Point>();
            var lockObject = new object(); // an object for locks
            
            Parallel.ForEach(files,
                             new ParallelOptions {MaxDegreeOfParallelism = 4},
                             file =>
                                 {
                                     if (file.EndsWith("txt")) return;
                                     var img = new Bitmap(Image.FromFile(file));
                                     var imgBytes = new int[img.Size.Width* img.Size.Height];
                                     
                                     for (var x = 0; x < img.Size.Width; x++)
                                         for (var y = 0; y < img.Size.Height; y++)
                                         {
                                             var color = img.GetPixel(x, y);
                                             imgBytes[x + 640 * y] = (int)(.299 * color.R + .587 * color.G + .114 * color.B);
                                         }

                                     int xCore = 0, yCore = 0;

                                     lock (lockObject)
                                     {
                                         findCorePoint(imgBytes, img.Size.Width, img.Size.Height, ref xCore, ref yCore);
                                         
                                         referencePointsAuto[Path.GetFileNameWithoutExtension(file)] = new Point(xCore,
                                                                                                                 yCore);
                                         images.Add(imgBytes,
                                                    int.Parse(Path.GetFileNameWithoutExtension(file).Split('_')[0]));
                                         referencePoints.Add(imgBytes, referencePointsAuto[Path.GetFileNameWithoutExtension(file)]);
                                     }
                                 });


            Dictionary<string, Point> referencePointsByFile = new Dictionary<string, Point>();
            using (var sr = new StreamReader(".\\Fingerprints\\cores.txt"))
            {
                string s;
                while (!string.IsNullOrEmpty(s = sr.ReadLine()))
                {
                    referencePointsByFile[Path.GetFileNameWithoutExtension(s)]= 
                        new Point(int.Parse(sr.ReadLine()),int.Parse(sr.ReadLine()));
                }
            }

            foreach (var file in referencePointsByFile.Keys)
            {
                var ptAuto = referencePointsAuto[file];
                var ptFile = referencePointsByFile[file];
                float dx = ptAuto.X - ptFile.X;
                float dy = ptAuto.Y - ptFile.Y;
                Console.WriteLine(
                    "{0}: {1}",
                    file, Math.Sqrt(dx*dx + dy*dy));
            }
            
            //var output = File.CreateText("C:\\temp\\results.csv");
            int M = 3; //number of bands
            int K = 24; //width of the band
            int L = 14; //radius of hole
            int N = 9; // number of sectors
            int P = 7; //number of filters
            int radius = M*K + L + 16; //mask size
            var suitableFingerprints =
                referencePoints.Keys.Where(key =>
                                               {
                                                   var p = referencePoints[key];
                                                   return
                                                       Math.Min(
                                                           Math.Min(p.X, 640 - p.X),
                                                           Math.Min(p.Y, 480 - p.Y)) >=
                                                       radius;
                                               }).ToList();

            var fingercodes = new Dictionary<float[], int>();

            foreach (var suitableFingerprint in suitableFingerprints)
            {
                        float[] fcode = new float[M*N*P];

                        createFingerCode(suitableFingerprint, fcode, 640, 480, P, M, N, L, K,
                                         referencePoints[suitableFingerprint].X,
                                         referencePoints[suitableFingerprint].Y);

                        fingercodes.Add(fcode, images[suitableFingerprint]);
            }
            var listedFCodes = fingercodes.ToList();
            var dbase = listedFCodes.SelectMany(x => x.Key).ToArray();

            loadNoncoalescedFingerCodeDatabase(dbase, M*N*P, suitableFingerprints.Count);
            int unmatched = 0;
            float[] matches = new float[listedFCodes.Count];
            foreach (var fcode in listedFCodes)
            {
                matchFingercode(fcode.Key, M * N * P, matches);
                for (int i = 0; i < listedFCodes.Count;i++ )
                {
                    var targetFcode = listedFCodes[i].Key;
                    float accum = 0;
                    for(int j=0;j<targetFcode.Length;j++)
                    {
                        var diff = fcode.Key[j] - targetFcode[j];
                        accum += diff*diff;
                    }
                    var delta = accum - matches[i];
                    if(delta>0.0001)
                        Console.WriteLine("PING");
                }
                var ordered = matches.Select((item, index) => Tuple.Create(item, listedFCodes[index])).OrderBy(x => x.Item1).Skip(1).
                    Select(x=>x.Item2.Value).Take(5).
                    ToList();

                if (!ordered.Contains(fcode.Value))
                {
                    Console.WriteLine(fcode.Value);
                    unmatched++;
                }
            }
            Console.WriteLine("{0} unsuccessful match(es)",unmatched);
            

            disposeDevice();
            Console.WriteLine("Done");
            Console.ReadKey();
        }

        private static void TestTimings()
        {
            var error = initDevice();
            if (error == 0)
            {
                int[] result = new int[640*480];
                var bmpSrc = new Bitmap(".\\Fingerprints\\108_5.tif");
                for (int x = 0; x < 640; x++)
                    for (int y = 0; y < 480; y++)
                    {
                        result[x + 640*y] = bmpSrc.GetPixel(x, y).R;
                    }

                int numBands = 3;
                int numSectors = 9;
                int holeRadius = 14;
                int filterAmount = 7;
                int bandRadius = 24;

                // this code was used to test the similarity of the filters
                //var filters = new float[32*32*filterAmount];
                //createGaborFilters(filters, 0.1f, filterAmount);

                //var min = filters.Min();
                //var max = filters.Max();

                //var filtersInt = filters.Select(x => (int) ((x - min)/(max - min)*255)).ToArray();

                //Common.SaveAndShowImage(Common.Convert1DArrayTo2D(filtersInt,32));

                int size = numBands*numSectors;

                int num = 1000000;

                float[] dbase = new float[size * num];
                var rand = new Random();
                for (int w = 0; w < num*size; w++)
                {
                    dbase[w] = (float)rand.NextDouble() * 128.0f;
                }
                Stopwatch sw = new Stopwatch();
                sw.Start();
                loadFingerCodeDatabase(dbase, size, num);
                sw.Stop();
                Console.WriteLine("Data loading took {0} ms", sw.ElapsedMilliseconds);
                long metaTotal = 0;
                using (FileStream fs = new FileStream("C:\\temp\\samsung_results.csv", FileMode.Create))
                {
                    using (StreamWriter streamWriter = new StreamWriter(fs))
                    {
                        streamWriter.WriteLine("Find;Create;Match;Top");
                        for (int i = 1; i <= 20; i++)
                        {
                            
                            Console.WriteLine("Try {0}", i);
                            Console.WriteLine();
                            long totalTime = 0;
                            sw.Restart();
                            int xCore = 0;
                            int yCore = 0;
                            findCorePoint(result, 640, 480, ref xCore, ref yCore);
                            //var variance = filterWithGaborFilter(result, 640, 480, 8, 5, 20, 20, 320, 240);
                            sw.Stop();
                            totalTime += sw.ElapsedMilliseconds;
                            streamWriter.Write(sw.ElapsedMilliseconds+";");
                            Console.WriteLine("Finding reference point took {0} ms", sw.ElapsedMilliseconds);

                            int[] fullImage = new int[640*480*filterAmount];

                            //var normalizedImage = new int[640*480];
                            //normalizeImage(result, normalizedImage, 640, 480, numBands, holeRadius, bandRadius, xCore, yCore);
                            //Common.SaveAndShowImage(Common.Convert1DArrayTo2D(normalizedImage, 640));
                            //fullFilterImage(result,fullImage,640,480,filterAmount,numBands,holeRadius,bandRadius,xCore,yCore);
                            //Common.SaveAndShowImage(Common.Convert1DArrayTo2D(fullImage, 640));
                            var fCode = new float[filterAmount*numBands*numSectors];
                            sw.Restart();
                            createFingerCode(result, fCode, 640, 480, filterAmount, numBands, numSectors, holeRadius,
                                             bandRadius,
                                             xCore, yCore);
                            sw.Stop();

                            //Common.SaveFingerCode(fCode.Select(x=>(double)x).ToList(),numBands,numSectors,filterAmount,bandRadius,holeRadius);
                            totalTime += sw.ElapsedMilliseconds;
                            streamWriter.Write(sw.ElapsedMilliseconds + ";");
                            Console.WriteLine("Creating FingerCode took {0} ms", sw.ElapsedMilliseconds);
                            //matching

                            float[] matchResult = new float[num];


                            sw.Restart();
                            matchFingercode(fCode, size, matchResult);
                            sw.Stop();
                            totalTime += sw.ElapsedMilliseconds;
                            streamWriter.Write(sw.ElapsedMilliseconds + ";");
                            Console.WriteLine("Matching took {0} ms", sw.ElapsedMilliseconds);
                            sw.Restart();
                            var matchList =
                                matchResult.Select((item, index) => Tuple.Create(item, index)).OrderBy(x => x.Item1).
                                    ToList();
                            sw.Stop();
                            totalTime += sw.ElapsedMilliseconds;
                            streamWriter.WriteLine(sw.ElapsedMilliseconds);
                            Console.WriteLine("Top selection {0} ms", sw.ElapsedMilliseconds);
                            Console.WriteLine("Total {0} ms", totalTime);
                            metaTotal += totalTime;
                        }
                    }
                }
                metaTotal /= 20;
                Console.WriteLine("Average total {0} ms", metaTotal);
            }


            disposeDevice();
            Console.WriteLine("Done");
            Console.ReadKey();
        }

        /*private static void TestHull()
        {
            Random r = new Random();
            int rows = r.Next();
            int columns = r.Next();
            Console.WriteLine("rows: {0}; columns: {1}", rows, columns);
            int NoM = r.Next();
            while (NoM == 0)
                NoM = r.Next();
            int[] arr = new int[2*NoM];
            for (int i = 0; i < NoM; i++)
            {
                arr[2*i] = r.Next();
                arr[2*i + 1] = r.Next();
                bool repeat = false;
                for (int j = i - 1; j >= 0; j--)
                {
                    if ((arr[2*i] == arr[2*j]) && (arr[2*i + 1] == arr[2*j + 1]))
                        repeat = true;
                }
                while (repeat)
                {
                    arr[2*i] = r.Next(0,rows-1);
                    arr[2*i + 1] = r.Next(0,columns-1);
                    repeat = false;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if ((arr[2*i] == arr[2*j]) && (arr[2*i + 1] == arr[2*j + 1]))
                            repeat = true;
                    }
                }
            }
            Console.WriteLine("Minutiae:");
            for (int i = 0; i < NoM; i++)
            {
                Console.WriteLine("{0} {1}", arr[2*i], arr[2*i + 1]);
            }
            int[] Hull = new int[2*NoM];
            int NHull = NoM;
            BuildHull(arr,NoM,Hull,NHull);
            Console.WriteLine("Hull:");
            for (int i = 0; i < NHull; i++)
            {
                Console.WriteLine("{0} {1}", Hull[2 * i], Hull[2 * i + 1]);
            }
        }*/
    }
}
