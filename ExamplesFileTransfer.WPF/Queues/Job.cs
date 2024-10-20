using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Examples.ExamplesFileTransfer.WPF.Queues
{
    public enum JobStatus
    {
        Complete,
        Error,
        Initial,
        Received,
        Sent
    }
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
        string DeviceId { get; set; }
        IJobComponentBasic Clone();
    }
    [Serializable]
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
        public string DeviceId { get; set; } = string.Empty;
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

        private static Stream ToStream(Job job)
        {
            var stream = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(stream, job);
            stream.Position = 0; // Đặt lại vị trí của stream về đầu
            return stream;
        }
        private static Job FromStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin); // Đặt lại vị trí của stream về đầu
            var formatter = new BinaryFormatter();
            var a =  (Job)formatter.Deserialize(stream);
            string gg = "";
            return a;
        }
        public static Job Receive(Stream stream) => FromStream(stream);
        public static Stream Send(Job job) => ToStream(job);
        public void StatusChange(JobStatus status)
        {
            switch (status)
            {
                case JobStatus.Error:
                    IsError = true;
                    IsHandled = false;
                    Error = "Error Handle";
                    break;
                case JobStatus.Complete:
                    IsHandled = true;
                    IsError = false;
                    Error = null;
                    break;
                case JobStatus.Initial:
                    IsHandled = false;
                    IsError = false;
                    Error = null;
                    break;
                case JobStatus.Received:
                    ReceiveTime = DateTime.Now;
                    IsHandled = true;
                    IsError = false;
                    Error = null;
                    break;
                case JobStatus.Sent:
                    SendTime = DateTime.Now;
                    ProcessTime = SendTime - ReceiveTime;
                    break;
                default:
                    break;
            }
        }
    }
}
