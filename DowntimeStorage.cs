using System;
using System.Collections.Generic;
namespace DowntimeOPC
{
    public class DowntimeStorage
    {
        private static List<Downtime> downtime = new List<Downtime>();

        public static void Add(Downtime _downtime)
        {
            if(ListExist_Downtime(_downtime)) return;

            downtime.Add(_downtime);
        }
        public static void Remove(Downtime _downtime)
        {
            if(IsEmpty()) return;

            downtime.RemoveAll(d => {
                return d.Line == _downtime.Line;
            });
        }

        public static void Update() {
            if(IsEmpty()) return;

            downtime.ForEach(d => {
                d.Update();
            });
        }

        public static void Print() {
            downtime.ForEach(d => {
                Console.WriteLine("Downtime: dateStart - {0}, dateEnd - {1}, Line - {2}, dTime - {3}", d.DateStart, d.DateEnd, d.Line, d.DTime);
            });  
        }

        private static bool IsEmpty()
        {
            return downtime.Count == 0;
        }

        private static bool ListExist_Downtime(Downtime _downtime)
        {
            bool ListExist = false;
            downtime.ForEach((d) => {
                if(ListExist) return;
                ListExist = d.Line == _downtime.Line;
            });
            return ListExist;
        }
    }
}