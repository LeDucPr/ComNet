using System;
using System.Security.Cryptography;
using System.Text;

namespace Examples.ExamplesFileTransfer.WPF.Queues.JobConfs
{
    public class UntGObj //: ‎MonoBehaviour -> Json Storage Format
    {
        private string m_Name; // tên đối tượng được spawn trong Game
        private string m_ObjectHideFlags; // ẩn đối tượng trong Unity (mở flag để hiển thị, Spawn ra trước lúc hệ thống đang rỗi)
        private string m_CorrespondingSourceObject; // đối tượng nguồn tương ứng (đóng từ trong Assets ra)
        private string m_PrefabInstance; // tham chiếu với Id prefab trong lưu trữ (fileId: 0)
        private string m_Component; // tham chiếu với Id component tại thời điểm spawn
        private string m_Layer; // lớp đối tượng
        private string m_TagString; // tag của đối tượng
        private string m_Icon; // icon của đối tượng
        private string m_NavMeshLayer; // lớp NavMesh của đối tượng
        private string m_StaticEditorFlags; // cờ chỉnh sửa tĩnh
        private string m_IsActive; // đối tượng có hoạt động không (sủa đổi trạng thái để chuyển từ Hive sang UnHive hoặc ngược lại), tương tác với HiveFlags
        ///// Transform:
        private string m_LocalRotation; // vị trí quay của đối tượng tương ứng khi Spawn
        private string m_LocalPosition; // vị trí của đối tượng tương ứng khi Spawn
        private string m_LocalScale; // tỉ lệ của đối tượng tương ứng khi Spawn
        private string m_Children; // danh sách các đối tượng con
        private string m_Father; // đối tượng cha
        private string m_RootOrder; // thứ tự của đối tượng
        private string m_LocalEulerAnglesHint; // gợi ý vị trí quay của đối tượng
        private Space space; // không gian bản đồ cho phép triển khai Spawn
        private string m_vsc; // điểu khiển dới vị trí mới 

        public string GOName { get => m_Name; set => m_Name = value; }
        public string GOHideFlags { get => m_ObjectHideFlags; set => m_ObjectHideFlags = value; }
        public string GOCorrespondingSourceObject { get => m_CorrespondingSourceObject; set => m_CorrespondingSourceObject = value; }
        public string GOPrefabInstance { get => m_PrefabInstance; set => m_PrefabInstance = value; }
        public string GOComponent { get => m_Component; set => m_Component = value; }
        public string GOLayer { get => m_Layer; set => m_Layer = value; }
        public string GOTagString { get => m_TagString; set => m_TagString = value; }
        public string GOIcon { get => m_Icon; set => m_Icon = value; }
        public string GONavMeshLayer { get => m_NavMeshLayer; set => m_NavMeshLayer = value; }
        public string GOStaticEditorFlags { get => m_StaticEditorFlags; set => m_StaticEditorFlags = value; }
        public string GOIsActive { get => m_IsActive; set => m_IsActive = value; }
        ///// Transform:
        public string LocalRotation { get => m_LocalRotation; set => m_LocalRotation = value; }
        public string LocalPosition { get => m_LocalPosition; set => m_LocalPosition = value; }
        public string LocalScale { get => m_LocalScale; set => m_LocalScale = value; }
        public string Children { get => m_Children; set => m_Children = value; }
        public string Father { get => m_Father; set => m_Father = value; }
        public string RootOrder { get => m_RootOrder; set => m_RootOrder = value; }
        public string LocalEulerAnglesHint { get => m_LocalEulerAnglesHint; set => m_LocalEulerAnglesHint = value; }
        public Space Space { get => space; set => space = value; }
        public string VSC { get => m_vsc; set => m_vsc = value; }

        public UntGObj()
        {
            m_Name = "Crab";
            m_ObjectHideFlags = "0";
            m_CorrespondingSourceObject = "{fileID: 0}";
            m_PrefabInstance = "{fileID: 0}";
            m_Component = "{fileID: 4280776974045060641}";
            Random random = new Random(); 
            using (MD5 md5Hash = MD5.Create())
            {
                byte[] bytes = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(random.ToString()));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2"));
                m_Layer = builder.ToString(); // trúng trùng layer (Layer này không phải Layer phân tầng trong Engine mà là Layer quy ước đối tượng)
            }
            m_TagString = "Untagged"; // không gắn tag
            m_Icon = "{fileID: 0}";
            m_NavMeshLayer = "0";
            m_StaticEditorFlags = "0";
            m_IsActive = "1";
            /// Transform:
            m_LocalRotation = "{x: -0, y: -0, z: -0, w: 1}";
            m_LocalPosition = "{x: 0, y: 0, z: 0}";
            m_LocalScale = "{x: 1, y: 1, z: 1}";
            m_Children = "Untagged";
            m_Father = "Untagged";
            m_RootOrder = "0";
            m_LocalEulerAnglesHint = "{x: 0, y: 0, z: 0}";
            VSC = "{x: 0, y: 0}";
        }
        public override string ToString()
            => Newtonsoft.Json.JsonConvert.SerializeObject(this);
        public static UntGObj FromString(string json)
            => Newtonsoft.Json.JsonConvert.DeserializeObject<UntGObj>(json);

    }
}
