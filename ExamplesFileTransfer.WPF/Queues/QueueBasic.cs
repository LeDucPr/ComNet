using System;
using System.Collections.Generic;
using System.Linq;

namespace Examples.ExamplesFileTransfer.WPF.Queues
{
    public interface IQueueBasic
    {
        void Enqueue(ReceivedFile rf, bool isReplace = true);
        ReceivedFile Dequeue();
        ReceivedFile Peek();
        int Count();
        void Clear();
    }
    public class QueueBasic : IQueueBasic
    {
        private List<ReceivedFile> _receivedFiles;
        public List<ReceivedFile> ReceivedFiles => _receivedFiles;
        public QueueBasic() { _receivedFiles = new List<ReceivedFile>(); }
        public virtual void Enqueue(ReceivedFile rf, bool isReplace = true)
        {
            if (!isReplace && _receivedFiles.Any(rs => rs.Filename == rf.Filename))
                throw new InvalidOperationException($"'{rf.Filename}' đã tồn tại trong hàng đợi.");
            else if (isReplace)
            {
                int index = GetIndexObjectByName(rf.Filename);
                if (index != -1)
                    _receivedFiles[index] = rf;
                else
                    _receivedFiles.Add(rf);
            }
        }
        public virtual ReceivedFile Dequeue()
        {
            if (_receivedFiles.Count == 0)
                throw new InvalidOperationException("Hàng đợi trống.");
            ReceivedFile job = _receivedFiles[0];
            _receivedFiles.RemoveAt(0);
            return job;
        }
        public virtual ReceivedFile Peek()
        {
            if (_receivedFiles.Count == 0)
                throw new InvalidOperationException("Hàng đợi trống.");
            return _receivedFiles[0];
        }
        public int Count() => _receivedFiles.Count;
        public void Clear() => _receivedFiles.Clear();
        public ReceivedFile GetObjectByName(string name) => _receivedFiles.FirstOrDefault(rs => rs.Filename == name);
        public int GetIndexObjectByName(string name) => _receivedFiles.FindIndex(rs => rs.Filename == name);
        public ReceivedFile this[int index]
        {
            get => _receivedFiles[index];
            protected set => _receivedFiles[index] = value;
        }
    }
}
