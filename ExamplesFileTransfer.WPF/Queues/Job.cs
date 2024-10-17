using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Examples.ExamplesFileTransfer.WPF.Queues
{
    public interface IJobComponentBasic
    {
        DateTime SendTime { get; set; } // thời gian gửi job
        DateTime ReceiveTime { get; set; } // thời gian nhận job
        TimeSpan ProcessTime { get; set; } // thời gian xử lý job
        bool IsError { get; set; } // lỗi trong phần xử lý
        string Error { get; set; } // thông báo lỗi nếu có
        string Message { get; set; } // thông báo nếu có
        bool IsLocked { get; } // trạng thái khóa job
        bool IsHandled { get; set; }
        IJobComponentBasic Clone();
    }
    public class Job : IJobComponentBasic
    {
        ///// interface IJobComponentBasic
        public string Name { get; set; } = string.Empty;
        public DateTime SendTime { get; set; } = DateTime.Now;
        public DateTime ReceiveTime { get; set; } = DateTime.Now;
        public TimeSpan ProcessTime { get; set; }
        public bool IsError { get; set; } = false;
        public string Error { get; set; } = null;
        public string Message { get; set; } = null;
        public bool IsHandled { get; set; } = false;
        public bool IsLocked
        {
            get
            {
                if ((!IsError) || (ReceiveTime > SendTime)) return false;
                return true;
            }
        }
        public string Data = string.Empty;
        public Job(string data) // thiết lập để truyền đi 
        {
            if (string.IsNullOrEmpty(data))
            {
                IsError = true;
                Error = "Dữ liệu rỗng";
                return;
            }
            else
            {
                Data = data;
                this.CreateJobParameters();
            }
        }
        private void CreateJobParameters()
        {
            Name = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            // băm hash512 cho dtPkName
            using (SHA512 sha512 = SHA512.Create())
            {
                byte[] hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(Name));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                    sb.Append(b.ToString("x2"));
                Name = sb.ToString();
            }
        }
        public IJobComponentBasic Clone() { return (Job)this.MemberwiseClone(); }
        public override string ToString() => Newtonsoft.Json.JsonConvert.SerializeObject(this);
        private static Job FromString(string jsonJob) => Newtonsoft.Json.JsonConvert.DeserializeObject<Job>(jsonJob);
        public static Job Receive(string data)
        {
            Job job = FromString(data);
            job.ReceiveTime = DateTime.Now;
            return job;
        }
        public static string Send(Job job, bool isHandled = false) // json job
        {
            job.SendTime = DateTime.Now;
            job.ProcessTime = job.SendTime - job.ReceiveTime;
            job.IsHandled = isHandled;
            return job.ToString();
        }
    }
}
