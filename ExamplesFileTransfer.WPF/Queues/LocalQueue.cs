using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples.ExamplesFileTransfer.WPF.Queues
{
    /// <summary>
    /// Chỉ ấp dụng cho các thành phần nội bộ 
    /// </summary>
    public interface ILocalQueue
    {
        bool Status { get; }
        bool IsCompletedSetup { get; }
        void Cancel(); // hủy bỏ toàn bộ hàng đợi
    }
    public class LocalQueue : QueueBasic, ILocalQueue
    {
        private bool status = false;
        private bool isCompletedSetup = false;
        public bool Status => status;
        public bool IsCompletedSetup => isCompletedSetup;
        public LocalQueue() : base() { }
        public void Cancel() => base.Clear();
    }
}
/// các máy giữ hàng đợi này riêng biệt và không cần phải đồng bộ, khi xử lý xong có thể clear 