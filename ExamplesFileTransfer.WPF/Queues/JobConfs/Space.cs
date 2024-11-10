using System;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Examples.ExamplesFileTransfer.WPF.Queues.JobConfs
{
    public class Space // : ‎MonoBehaviour -> Không gian bản đồ cho phép triển khai Spawn 
    {
        private string m_Name; // tên đối tượng được spawn trong Game
        private string m_ObjectHideFlags; // ẩn đối tượng trong Unity (mở flag để hiển thị, Spawn ra trước lúc hệ thống đang rỗi)
        private string m_CorrespondingSourceObject; // đối tượng nguồn tương ứng (đóng từ trong Assets ra)
        private string m_PrefabInstance; // tham chiếu với Id prefab trong lưu trữ (fileId: 0)
        private string m_Component; // tham chiếu với Id component tại thời điểm spawn
        private string m_Layer; // lớp đối tượng
        private string m_Icon; // icon của đối tượng
        private string m_NavMeshLayer; // lớp NavMesh khung cho phép đặt NavMesh của Children lên 
        private string m_StaticEditorFlags; // cờ chỉnh sửa tĩnh (không cho phép Spawn)
        private string m_IsActive; // đối tượng (Khung hình hoạt động)
        private string m_limitSpawn; // giới hạn số lượng đối tượng được spawn ra
        private string m_limitSpace;
        private List<string> m_SpawnLocations; // vị trí spawn của đối tượng được chỉ định

        public string GOName { get => m_Name; set => m_Name = value; }
        public string GOHideFlags { get => m_ObjectHideFlags; set => m_ObjectHideFlags = value; }
        public string GOCorrespondingSourceObject { get => m_CorrespondingSourceObject; set => m_CorrespondingSourceObject = value; }
        public string GOPrefabInstance { get => m_PrefabInstance; set => m_PrefabInstance = value; }
        public string GOComponent { get => m_Component; set => m_Component = value; }
        public string GOLayer { get => m_Layer; set => m_Layer = value; }
        public string GOIcon { get => m_Icon; set => m_Icon = value; }
        public string GONavMeshLayer { get => m_NavMeshLayer; set => m_NavMeshLayer = value; }
        public string GOStaticEditorFlags { get => m_StaticEditorFlags; set => m_StaticEditorFlags = value; }
        public string GOIsActive { get => m_IsActive; set => m_IsActive = value; }
        public string GOLimitSpawn { get => m_limitSpawn; set => m_limitSpawn = value; }
        public string GOLimitSpace { get => m_limitSpace; set => m_limitSpace = value; }
        public List<string> GOSpawnLocations { get => m_SpawnLocations; set => m_SpawnLocations = value; }
        public Space()
        {
            // khởi tạo giá trị mặc định
            m_Name = "Space";
            m_ObjectHideFlags = "0";
            m_CorrespondingSourceObject = "{fileID: 0}";
            m_PrefabInstance = "{fileID: 0}";
            m_Component = "{fileID: 0}";
            m_Layer = "0";
            m_Icon = "{fileID: 0}";
            m_NavMeshLayer = "0";
            m_StaticEditorFlags = "0";
            m_IsActive = "1";
            m_limitSpawn = "{limit:100}";
            // tham số trong giới hạn transform
            m_limitSpace = "{x: 0, y: 0, h: 100, w:100}";
            m_SpawnLocations = new List<string>();
            // add vài thằng vào trong List // Các hệ số Spawn theo đường chéo
            m_SpawnLocations.Add("{x: 10, y: 20}");
            m_SpawnLocations.Add("{x: 20, y: 30}");
            m_SpawnLocations.Add("{x: 30, y: 40}");
            m_SpawnLocations.Add("{x: 40, y: 50}");
            m_SpawnLocations.Add("{x: 50, y: 60}");
            m_SpawnLocations.Add("{x: 60, y: 70}");
            m_SpawnLocations.Add("{x: 70, y: 80}");
        }
    }

    public static class SpaceExtension
    {
        public static UntGObj Spawn(this Space space)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));

            // Random trong list m_SpawnLocations
            Random random = new Random();
            int index = random.Next(space.GOSpawnLocations.Count);
            string spawnLocation = space.GOSpawnLocations[index];
            // Phân tích cú pháp chuỗi spawnLocation để lấy x và y
            var coordinates = spawnLocation.Trim('{', '}').Split(',');
            var x = coordinates[0].Split(':')[1].Trim();
            var y = coordinates[1].Split(':')[1].Trim();
            string newPosition = $"{{x: {x}, y: {y}, z: 0}}";
            return new UntGObj { LocalPosition = newPosition, Space = space };
        }
        public static void Handler(this UntGObj gObj)
        {
            if (gObj == null) throw new ArgumentNullException(nameof(gObj));
            // Phân tích cú pháp VSC để lấy x và y
            var vscCoordinates = gObj.VSC.Trim('{', '}').Split(',');
            var vscX = float.Parse(vscCoordinates[0].Split(':')[1].Trim());
            var vscY = float.Parse(vscCoordinates[1].Split(':')[1].Trim());
            // Phân tích cú pháp LocalPosition để lấy x, y, và z
            var localPositionCoordinates = gObj.LocalPosition.Trim('{', '}').Split(',');
            var localX = float.Parse(localPositionCoordinates[0].Split(':')[1].Trim());
            var localY = float.Parse(localPositionCoordinates[1].Split(':')[1].Trim());
            var localZ = float.Parse(localPositionCoordinates[2].Split(':')[1].Trim());
            // Phân tích cú pháp m_limitSpace để lấy các giới hạn x, y, h, và w
            var limitSpaceCoordinates = gObj.Space.GOLimitSpace.Trim('{', '}').Split(',');
            var limitX = float.Parse(limitSpaceCoordinates[0].Split(':')[1].Trim());
            var limitY = float.Parse(limitSpaceCoordinates[1].Split(':')[1].Trim());
            var limitH = float.Parse(limitSpaceCoordinates[2].Split(':')[1].Trim());
            var limitW = float.Parse(limitSpaceCoordinates[3].Split(':')[1].Trim());
            // Tính vị trí mới của đối tượng
            var newX = localX + vscX;
            var newY = localY + vscY;
            // Không vượt quá m_limitSpace
            if (newX < limitX) newX = limitX;
            if (newX > limitX + limitW) newX = limitX + limitW;
            if (newY < limitY) newY = limitY;
            if (newY > limitY + limitH) newY = limitY + limitH; 
            gObj.LocalPosition = $"{{x: {newX}, y: {newY}, z: {localZ}}}"; // Gán vị trí mới 
        }
    }
}
