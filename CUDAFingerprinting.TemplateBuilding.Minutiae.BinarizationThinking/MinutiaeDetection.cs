﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using CUDAFingerprinting.Common;

namespace CUDAFingerprinting.TemplateBuilding.Minutiae.BinarizationThinking
{
    public static class MinutiaeDetection
    {
        public static int CheckMinutiae(double[,] area) //Данный метод определяет, является ли пиксель минуцией, result = 0 - не минуция, иначе - минуция
        {                                               //На вход дается окрестность пикселя
            int result; // 1 - ending, >2 - branching,                     
            int counter = 0;
            area[1, 1] = 255;
            for (int i = 0; i < area.GetLength(0); i++)
            {
                for (int j = 0; j < area.GetLength(1); j++)
                {
                    if (area[i, j] == 0) counter++;
                }
            }
            if (counter == 1)
            {
                return result = 1;
            }
            else
            {
                if (counter > 2)
                {
                    return result = counter;
                }
                else
                {
                    return result = 0;
                }
            }
        }
 
        public static List<Minutia> FindMinutiae(double[,] picture)
        {
            List<Minutia> minutiae = new List<Minutia>();
            double[,] area = new double[3, 3];
            for (int k = 0; k < 3; k++)
            {
                for (int l = 0; l < 3; l++)
                {
                    area[k, l] = 255;
                }
            }
            double[,] newPicture = new double[picture.GetLength(0) + 2, picture.GetLength(1) + 2];
            for (int i = 0; i < newPicture.GetLength(0); i++)
            {
                for (int j = 0; j < newPicture.GetLength(1); j++)
                {
                    newPicture[i, j] = 255;
                }
            }
            for (int i = 1; i < newPicture.GetLength(0) - 1; i++)
            {
                for (int j = 1; j < newPicture.GetLength(1) - 1; j++)
                {
                    newPicture[i, j] = picture[i - 1, j - 1];                              //Вставляем входную картинку в более широкий массив для удобства рассмотрения граничных случаев 
                }
            }
            for (int i = 1; i < newPicture.GetLength(0) - 1; i++)
            {
                for (int j = 1; j < newPicture.GetLength(1) - 1; j++)
                {
                    if (newPicture[i, j] == 0)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            for (int l = 0; l < 3; l++)
                            {
                                area[k, l] = newPicture[i - 1 + k, j - 1 + l];    //Проходим по массиву и проверяем для каждого черного пикселя, является ли он минуцией. 
                            }
                        }
                        if (CheckMinutiae(area) > 0)
                        {
                            Minutia newMinutiae = new Minutia();                 //Если да, то добавляем минуцию в стек
                            newMinutiae.X = j;
                            newMinutiae.Y = i;
                            minutiae.Add(newMinutiae);
                        }
                    }
                }
            }
            return minutiae;
        }

        public class SpecialComparer : IComparer<List<MinutiaSpecial>>
        {
            public int Compare(List<MinutiaSpecial> list1, List<MinutiaSpecial> list2)
            {
                int result = 1;
                if (list1.Count > list2.Count)
                    result = -1;
                if (list1.Count == list2.Count)
                    result = 0;
                return result;
            }
        }

        public static List<Minutia> FindBigMinutiae(List<Minutia> listMinutiae)
        {
            List<MinutiaSpecial> listMinutiaSpecial = new List<MinutiaSpecial>();
            foreach (var minutia in listMinutiae)
            {
                MinutiaSpecial minutiaS = new MinutiaSpecial();
                minutiaS.X = minutia.X;
                minutiaS.Y = minutia.Y;
                minutiaS.belongToBig = false;
                minutiaS.numberMinutiaeInCircle = 0;
                listMinutiaSpecial.Add(minutiaS);
            }
            int dY;
            int dX;
            int Radius = 60;
            List<List<MinutiaSpecial>> listBigMinutiae = new List<List<MinutiaSpecial>>();
            for (int i = 0; i < listMinutiaSpecial.Count; i++)
            {
                List<MinutiaSpecial> listSmallMinutiae = new List<MinutiaSpecial>();
                for (int j = 0; j < listMinutiaSpecial.Count; j++)
                {
                    dX = listMinutiaSpecial[i].X - listMinutiaSpecial[j].X;
                    dY = listMinutiaSpecial[i].Y - listMinutiaSpecial[j].Y;
                    if ((dX * dX + dY * dY) < Radius * Radius)
                    {
                        //var temp = listMinutiaSpecial[j];
                        //temp.belongToBig = true;
                        //listMinutiaSpecial[j] = temp;
                        //var temp = listMinutiaSpecial[j];
                        //temp.belongToBig = true;
                        //listMinutiaSpecial[j] = temp;

                        listSmallMinutiae.Add(listMinutiaSpecial[j]);
                    }
                }
                listBigMinutiae.Add(listSmallMinutiae);
            }
            SpecialComparer comparer = new SpecialComparer();
            listBigMinutiae.Sort(comparer);
            List<Minutia> newListMinutiae = new List<Minutia>();
            for (int i = 0; i < listBigMinutiae.Count; i++)
            {
                Minutia newMinutia = new Minutia();
                newMinutia.X = 0;
                newMinutia.Y = 0;

                for (int j = 0; j < listBigMinutiae[i].Count; j++)
                {
                    if (listBigMinutiae[i][j].belongToBig == false)
                    {
                        newMinutia.X += listBigMinutiae[i][j].X;
                        newMinutia.Y += listBigMinutiae[i][j].Y;
                        var temp = listBigMinutiae[i][j];
                        temp.belongToBig = true;
                        listBigMinutiae[i][j] = temp;
                    }
                }
                newMinutia.X = (newMinutia.X + listBigMinutiae[i].Count - 1) / listBigMinutiae[i].Count;
                newMinutia.Y = (newMinutia.Y + listBigMinutiae[i].Count - 1) / listBigMinutiae[i].Count;
                newListMinutiae.Add(newMinutia);
            }
            return newListMinutiae;
        }
    }
}
