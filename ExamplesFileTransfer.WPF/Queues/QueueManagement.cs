using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Examples.ExamplesFileTransfer.WPF.Queues.JobConfs;
using UnitySpace = Examples.ExamplesFileTransfer.WPF.Queues.JobConfs.Space;
using UnityJob = Examples.ExamplesFileTransfer.WPF.Queues.JobConfs.UntGObj;

namespace Examples.ExamplesFileTransfer.WPF.Queues
{
    public class QueueManagement
    {
        private GlobalQueue _gQueue;
        private LocalQueue _lQueue;
        private List<string> _jobNeedToHandler; // chỉ chứa tên các job cần xử lý, xử lý xong thì đá ra  
        public void AddTo_JobNeedToHandler(string jobName) { _jobNeedToHandler.Add(jobName); }
        private List<string> _gQueueLogJobReceive; // lưu lại các job nhận được từ global queue
        private const int _maxLog = 100;
        private int _maxSubmissionCanSendForAJob = 5; // số lần gửi lại cho một job
        public int MaxSubmissionCanSendForAJob { set => _maxSubmissionCanSendForAJob = value; get => _maxSubmissionCanSendForAJob; }
        public GlobalQueue GQueue => _gQueue;
        public LocalQueue LQueue => _lQueue;
        private int _maxJobsInLocalQueue = 500;
        private CancellationTokenSource _cancellationTokenSource; // hủy task khi tắt 
        public CancellationTokenSource CancellationTokenSource { set => _cancellationTokenSource = value; }
        private Task _transferGlobalToLocalTask;
        private Task _transferLocalToGlobalTask;
        int _delay = 0;
        // Log 
        private string _gQueueLog = string.Empty;
        private string _lQueueLog = string.Empty;
        public string GQueueLog => _gQueueLog;
        public string LQueueLog => _lQueueLog;
        // Device Param
        public int MaxClone => _gQueue.MaxCloneJob; // biến này để kiểm soát lượt gửi qua các máy khác nhau (chỉ áp dụng cho job chửa xử lý)
        private string _deviceId;
        public string DeviceId
        {
            set // lấy mã của thằng rsa để tăng tính ngẫu nhiên khi tạo id máy trên các cấu hình khác nhau 
            {
                using (RSA rsa = RSA.Create())
                {
                    byte[] randomBytes = new byte[32];
                    using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                    { rng.GetBytes(randomBytes); }
                    byte[] encryptedBytes = rsa.Encrypt(randomBytes, RSAEncryptionPadding.OaepSHA256);
                    _deviceId = Convert.ToBase64String(encryptedBytes);
                }
            }
            get => _deviceId;
        }

        // Unity Job Handler + Spawner 
        UnitySpace _unitySpace = new UnitySpace(); // thiết lập mặc định

        public QueueManagement()
        {
            _cancellationTokenSource = new CancellationTokenSource(); 
            _gQueue = new GlobalQueue();
            _lQueue = new LocalQueue();
            _jobNeedToHandler = new List<string>();
            _gQueueLogJobReceive = new List<string>();
            _transferGlobalToLocalTask = Task.Run(() => TransferJobsFromGlobalToLocal(_cancellationTokenSource.Token));
            // Kiến trúc thay đổi được truyền thẳng từ local queue sang TCP sender ????
            //_transferLocalToGlobalTask = Task.Run(() => TransferJobsFromLocalToGlobal(_cancellationTokenSource.Token)); 
            _transferLocalToGlobalTask = Task.Run(() => ManagerSpawner(_cancellationTokenSource.Token));
            _transferLocalToGlobalTask = Task.Run(() => ManagerHandler(_cancellationTokenSource.Token));
        }

        private void TransferJobsFromGlobalToLocal(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested || cancellationToken == default)
            {
                if (_lQueue.Count() < _maxJobsInLocalQueue && _gQueue.Count() > 0) // hàng đợi bên ngoài chứa và số lượng Job chưa đủ 
                {
                    int count = _lQueue.Count();
                    var job = _gQueue.Dequeue();
                    if (!job.IsHandled || (job.IsError && job.IsHandled)) // chưa xử lú hoặc gặp lỗi khi xuwrlys từ Ip khác
                    {
                        _lQueue.Enqueue((Job)job.Clone()); // clone để không ảnh hưởng đến việc xóa trong việc xóa tập tin nhận được 
                        JobHandler(job);
                        if (_delay != 0)
                            Task.Delay(_delay).Wait();
                    }
                    if (count != _lQueue.Count()) // xét số lượng trước và sau, nếu có biến động thì mới thêm vào đối tượng cần xử lý và ghi log 
                    {
                        AddTo_JobNeedToHandler(job.Name);
                        _lQueueLog = $"-->> (LocalQueue): {job.Name + "  " + job.Message}";
                    }
                }
            }
        }

        private void TransferJobsFromLocalToGlobal(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested || cancellationToken == default)
            {
                if (_lQueue.Count() > 0)
                {
                    var job = _lQueue.Dequeue();
                    if ((job?.IsHandled != null || job.IsHandled) || job.IsError)
                        _gQueue.Enqueue(job);
                    if (_delay != 0)
                        Task.Delay(_delay).Wait();
                }
            }
        }
        #region Queue Manager 
        /// <summary>
        /// Xử lý xong đẩy lại vào local queue theo kiến trúc mới 
        /// </summary>
        /// <param name="cancellationToken"></param>
        private void ManagerHandler(CancellationToken cancellationToken, bool isPrioritizeReceiveJob = true)
        {
            while (!cancellationToken.IsCancellationRequested || cancellationToken == default)
            {
                if (_jobNeedToHandler.Count() != 0)
                {
                    if (isPrioritizeReceiveJob)
                    {
                        _lQueue.SetJobToFirst(_jobNeedToHandler.First());
                        _jobNeedToHandler.RemoveAt(0);
                    }
                    //if (_lQueue.Peek().DeviceId != _deviceId) // cần tính tới tường hợp Job spawn ra xếp trước thì không xử lý được
                    {
                        Job job = _lQueue.Dequeue();
                        if (JobHandler(job))
                            job.StatusChange(JobStatus.Complete);
                        else
                            job.StatusChange(JobStatus.Error);
                        _lQueue.Enqueue(job);
                    }
                }
            }
        }
        /// <summary>
        /// Tạo job và đẩy vào local queue theo kiến trúc mới cho phép hoạt động độc lập thành 2 phiên bản
        /// </summary>
        /// <param name="cancellationToken"></param>
        private void ManagerSpawner(CancellationToken cancellationToken)
        {
            for (int i = 2; i < 6; i++)
            {
                Job spjob = JobSpawner(_unitySpace.Spawn().ToString());
                //Job spjob = JobSpawner(i.ToString()); // Job đơn giản cấu hình mặc định
                _lQueue.Enqueue(spjob);
                _jobNeedToHandler.Add(spjob.Name);
                Task.Delay(100).Wait(); // tránh spawn fff bị ghi đè job
            }
        }
        /// <summary>
        /// Cho job vào global queue và lưu lại tên job đã nhận, xóa tên job cũ nếu vượt quá giới hạn
        /// </summary>
        /// <param name="job"></param>
        public void TransferJobToGlobalQueue(Job job)
        {
            bool isReplace = false;
            if (_gQueueLogJobReceive.Contains(job.Name))
                if (job.IsHandled)
                    isReplace = true;
            _gQueue.Enqueue(job, isReplace);
            //if ((job.IsHandled || job.IsError) || job.DeviceId != _deviceId)
            _gQueueLogJobReceive.Add(job.Name);
            if (!isReplace)
                _jobNeedToHandler.Add(job.Name);
            if (_gQueueLogJobReceive.Count() > _maxLog)
                _gQueueLogJobReceive.RemoveAt(0);
        }
        #endregion 

        #region Job Handler
        private bool JobHandler(Job job)
        {
            string data = job.Data;
            try
            {
                // chuyển sang unityJob 
                UnityJob unityJob = UnityJob.FromString(data);
                try
                {
                    unityJob.Handler();
                    job.Message = "Xử lý hoàn tất";
                    return true;
                }
                catch
                {
                    job.Error = "Lỗi xử lý";
                    job.Message = "Lỗi xử lý";
                    return false;
                }
            }
            catch
            {
                job.Message = "Dữ liệu không đúng định dạng";
                job.Error = "Lỗi định dạng";
                return false;
            }

            //string data = job.Data;
            //if (int.TryParse(data, out int result))
            //{
            //    job.Message = $"{result} không phải là số nguyên tố";
            //    for (int i = 2; i <= Math.Sqrt(result); i++)
            //        if (result % i == 0)
            //            return false;
            //    job.Message = $"{result} là số nguyên tố";
            //    return true;
            //}
            //job.Message = "Dữ liệu không phải là số";
            //job.Error = "Lỗi định dạng";
            //return false;
        }
        private Job JobSpawner(string data)
        {
            Job job = new Job(data);
            job.DeviceId = _deviceId;
            job.StatusChange(JobStatus.Initial);
            return job;
        }
        #endregion

        #region Transfer Job To TCP Sender
        public Job TransferJobToTCPSender_Job()
        {
            if (_lQueue.Count() > 0)
                return _lQueue.Dequeue();
            return null;
        }
        public (string, Job) TransferJobToTCPSender_Job_Name()
        {
            if (_lQueue.Count() > 0)
            {
                var job = _lQueue.Dequeue();
                return (job.Name, job);
            }
            return (string.Empty, null);
        }
        public (string, Stream) TransferJobToTCPSender_Job_Stream()
        {
            if (_lQueue.Count() > 0)
            {
                var job = _lQueue.Dequeue();
                return (job.Name, Job.Send(job));
            }
            return (string.Empty, null);
        }
        #endregion Transfer Job To TCP Sender


        public void SetJobToFirstInLocalQueue(string jobName) => _lQueue.SetJobToFirst(jobName);
        public List<string> FindSpawnerJob() => _lQueue.Jobs.FindAll(x => x.DeviceId == _deviceId).Select(x => x.Name).ToList();
        public List<string> FindHandlerJob() => _lQueue.Jobs.FindAll(x => x.DeviceId != _deviceId).Select(x => x.Name).ToList();

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            Task.WaitAll(_transferGlobalToLocalTask, _transferLocalToGlobalTask);
        }
    }
}
