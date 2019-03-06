using System;

namespace DowntimeOPC
{
    public class Downtime
    {
        private string line;
        private DateTime dateStart;
        private DateTime dateEnd;
        private int dTime;
       
        public string Line { get{ return line; } }
        public DateTime DateStart { get{ return dateStart; } }
        public DateTime DateEnd { get { return dateEnd; } }
        public int DTime { get{ return dTime; } }

        public Downtime(string _line)
        {
            line = _line;
            dateStart = DateTime.Now;
            dTime = 0;
        }

        public void Update()
        {
            dTime = ConvertTime_to_Int(DateTime.Now - dateStart);
        }

        public void Close()
        {
            dateEnd = DateTime.Now;
        }

        private int ConvertTime_to_Int(TimeSpan _time)
        {
            return (int) _time.TotalSeconds;
        }
    }
}