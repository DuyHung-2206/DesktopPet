# BÁO CÁO TOÀN DIỆN VỀ ĐỒNG BỘ HOẠT ẢNH PHỤ KIỆN (ACCESSORY ANIMATION SYNC REPORT)
**Dự án:** DesktopPetWorld (WPF .NET 8.0)  
**Ngày thực hiện:** 14/09/2026  

---

## 1. TẬP HỢP TỆP ĐÃ THAY ĐỔI & RÀ SOÁT (FILES CHANGED & AUDITED)

1. [Views/Controls/PetRenderer.xaml.cs](file:///c:/Users/admin/OneDrive/Desktop/DesktopPet/Views/Controls/PetRenderer.xaml.cs):
   - **Xóa bỏ triệt để hardcoded item ID**: Hoàn toàn không chứa bất kỳ logic `if (itemId == "cap_cool")` hay `if (itemId == "hat_top")`. Việc lấy slot và item definition được thực hiện tổng quát qua `pet.GetEquippedItem(slot)` và `DataManager.Instance.GetItem(itemId)`.
   - **Thống nhất Master Clock**: Duy nhất `_spriteTimer` đóng vai trò nguồn xung nhịp thời gian duy nhất cho cả thú cưng và 4 phụ kiện. Không tồn tại bất kỳ DispatcherTimer độc lập nào cho phụ kiện.
   - **Snapshot trạng thái hoạt ảnh (`PetAnimationState`)**: Đóng gói `(AnimationName, State, FrameIndex, PetFrameCount, IsFlipped, PetScale, CellDimension)` thành đối tượng bất biến truyền qua hàm `RenderAnimationFrame`.
   - **Thuật toán ánh xạ frame (`MapFrameIndex`)**: Chuyển đổi tỷ lệ toán học nguyên thủy Index_acc = floor(Index_pet * Count_acc / Count_pet) và kẹp trong khoảng [0, Count_acc - 1].
   - **Rà soát & thay thế toàn bộ Modulo cứng (`% 5`, `% 6`)**: Thay thế bằng `Math.Clamp` theo đúng số lượng frame thực tế được trích xuất từ sprite sheet của từng hành động.
   - **Ngữ nghĩa PetState thay vì kiểm tra pixel (`cellDim == 96`)**: Chuyển sang kiểm tra ngữ nghĩa `state == PetState.Eat || state == PetState.Drink`.
   - **Bảo toàn Frame khi đổi hướng hoặc trang bị (Step 7)**: Nhận diện `isSameStateAndFile`, cập nhật hướng lật và phụ kiện mà **không** đặt lại `_currentFrameIndex = 0`.

2. [Views/Controls/PetRenderer.xaml](file:///c:/Users/admin/OneDrive/Desktop/DesktopPet/Views/Controls/PetRenderer.xaml):
   - Thứ tự phân lớp (Z-index / Layer Order) được kiểm chứng chính xác trong XAML:
     1. `BackpackOverlayImage` (phía sau thân pet)
     2. `SpriteImage` (thân pet)
     3. `HatOverlayImage` (tiền cảnh trên đầu pet)
     4. `GlassesOverlayImage` (tiền cảnh trên mắt pet)
     5. `BowOverlayImage` (tiền cảnh trên cổ pet)
   - Tất cả các lớp này đều nằm trong `RootGrid` có `ScaleTransform FlipScale`, tự động lật đồng bộ theo hướng nhìn của pet (`ScaleX = 1` hoặc `-1`).

3. [ViewModels/PetViewModel.cs](file:///c:/Users/admin/OneDrive/Desktop/DesktopPet/ViewModels/PetViewModel.cs):
   - Bổ sung `public double PetScale => _save.Settings?.PetScale ?? 1.0;`.
   - Giảm thiểu sự kiện thừa của `IsFacingLeft` để tránh trigger lặp không cần thiết.

4. [Views/PetWindow.xaml.cs](file:///c:/Users/admin/OneDrive/Desktop/DesktopPet/Views/PetWindow.xaml.cs):
   - Truyền `_viewModel.PetScale` vào `PetRendererControl.UpdateAppearance(...)`.
   - Đăng ký nhận thông báo khi `PetScale` thay đổi để đồng bộ kích thước khung vẽ.

5. [Models/EquippedItems.cs](file:///c:/Users/admin/OneDrive/Desktop/DesktopPet/Models/EquippedItems.cs) & [Models/Pet.cs](file:///c:/Users/admin/OneDrive/Desktop/DesktopPet/Models/Pet.cs):
   - Chuẩn hóa 4 slot độc lập: `Hat`, `Glasses`, `Bow`, `Backpack`.
   - Cơ chế thay thế slot: Trang bị cùng slot sẽ tự động thay thế item cũ; các slot khác nhau có thể trang bị đồng thời.

6. [Data/items.json](file:///c:/Users/admin/OneDrive/Desktop/DesktopPet/Data/items.json):
   - Xác thực đúng 7 phụ kiện theo quy chuẩn và bảo toàn toàn bộ hệ thống thức ăn (Food) và đồ chơi (Toy).

7. [scratch/test_outfit_system.cs](file:///c:/Users/admin/OneDrive/Desktop/DesktopPet/scratch/test_outfit_system.cs):
   - Bộ kiểm thử tự động mở rộng lên **223 bài test** bao phủ toàn bộ ma trận slot, kiểm chứng tệp tài nguyên, tỷ lệ frame, cô lập thức ăn/đồ chơi và không reset frame khi đang chạy hoạt ảnh.

---

## 2. NGUYÊN NHÂN GỐC CỦA VẤN ĐỀ LỆCH ĐỒNG BỘ (ROOT CAUSES)

1. **Hiện tượng giật & lệch frame do Reset sai thời điểm (`UpdateAppearance`)**:
   - Khi thú cưng đang bước đi (`Walk` frame 3 hay 4), nếu thú cưng đổi hướng quay mặt hoặc người chơi bấm trang bị/tháo đồ, `UpdateAppearance` trước đây gọi `LoadSpriteAnimation` và vô tình gán `_currentFrameIndex = 0`.
   - Hậu quả: Pet bị khựng giật về frame 0 trong khi phụ kiện hoặc vị trí di chuyển vẫn đang ở nhịp cũ, gây cảm giác phụ kiện bị bay lơ lửng, trôi dạt hoặc tách rời khỏi thân thể pet.
2. **Sai số do phụ thuộc vào kích thước ô ảnh (`cellDim == 96`)**:
   - Hàm xác định tọa độ gắn phụ kiện trước đây suy đoán trạng thái `Eat` bằng cách kiểm tra kích thước ô ảnh có bằng 96 pixel hay không. Nếu có một animation nào khác có kích thước tương tự hoặc animation `Eat` được scale, hệ thống sẽ rơi vào nhánh tính toán sai lệch.
3. **Phép chia lấy dư cứng (`% 5`, `% 6`) không an toàn**:
   - Các trạng thái ghép (ví dụ `Jump` ghép với `Run`, `Drink` ghép với `Eat`, `WakeUp` ghép với `Idle`) có thể có số frame logic khác với số frame vật lý. Phép chia lấy dư cứng khiến frame index bị quay vòng không kiểm soát.

---

## 3. THIẾT KẾ ĐỒNG BỘ HOẠT ẢNH (FRAME SYNCHRONIZATION DESIGN)

### 3.1. Master Clock duy nhất
- Duy nhất `_spriteTimer` trong `PetRenderer` đóng vai trò Master Clock. Toàn bộ 4 slot phụ kiện (`Hat`, `Glasses`, `Bow`, `Backpack`) hoàn toàn **không** có timer riêng.
- **Tính nguyên tử (Atomicity)**: Cả thân Pet và 4 phụ kiện được tính toán vị trí, góc xoay, tỷ lệ và vẽ lên màn hình trong cùng 1 chu kỳ render của WPF UI Dispatcher.

### 3.2. Thuật toán ánh xạ Frame (`MapFrameIndex`)
- Đối với phụ kiện dạng Horizontal Sprite Strip có số frame khác Pet:
  `accessoryIndex = Math.Clamp((int)((long)petFrameIndex * accessoryCount / petCount), 0, accessoryCount - 1)`
- Nếu Pet và Phụ kiện cùng số frame (N:N): Ánh xạ 1:1 hoàn hảo (0 -> 0, 1 -> 1, ...).
- Nếu Phụ kiện là ảnh đơn (1 frame): Luôn trả về index 0 và không crop ảnh.
- Không dùng phép nội suy theo thời gian trôi (elapsed time) hay bộ hẹn giờ thứ hai, triệt tiêu hoàn toàn độ trôi (drift).

### 3.3. Bảng neo tọa độ giải phẫu (Transform / Anchor Design)
Tọa độ phụ kiện được xác định chính xác theo từng frame cụ thể của từng hành động bằng `Math.Clamp`:
- **`Idle` / `Sit` / `WakeUp`**: 1 frame ổn định, offset (0, 0).
- **`Walk`** (6 frames: 0..5): Bám sát nhịp lắc lư đầu và chân mèo.
- **`Run` / `Jump`** (6 frames: 0..5): Khớp góc nghiêng thân và độ rướn khi chạy nước rút.
- **`Eat` / `Drink`** (5 frames: 0..4): Mỗi frame 1000ms, theo sát nhịp cúi đầu xuống đĩa ăn và ngẩng đầu lên.
- **`Bath` / `Dirty`** (6 frames: 0..5): Bám sát chuyển động lắc người và bong bóng xà phòng.
- **`Sleep`** (5 frames: 0..4): Bám sát nhịp thở và tư thế nằm gối đầu cuộn tròn.
- **`Happy` / `Play` / `Dance`** (6 frames: 0..5): Bám sát nhịp nhún nhảy vui vẻ.
- **`Hurt` / `Sick` / `Sad` / `Angry`** (5 frames: 0..4): Bám sát tư thế co cụm, rung run khi bị đau.

---

## 4. KẾT QUẢ XÁC MINH TÀI NGUYÊN (ASSET VALIDATION RESULTS)

Toàn bộ 7 tệp overlay đều hợp lệ tuyệt đối:

1. `hat_top`: Mũ ảo thuật gia, Slot=Hat, File=Assets/Clothing/Hats/wizard_hat.png, 64x64, RGBA, 92.8% transparent
2. `cap_cool`: Mũ snapback sành điệu, Slot=Hat, File=Assets/Clothing/Hats/snapback.png, 64x64, RGBA, 89.4% transparent
3. `crown_gold`: Vương miện hoàng gia, Slot=Hat, File=Assets/Clothing/Hats/crown.png, 64x64, RGBA, 96.9% transparent
4. `glasses_round`: Kính cận tri thức, Slot=Glasses, File=Assets/Clothing/Glasses/smart_glasses.png, 64x64, RGBA, 97.9% transparent
5. `sunglasses_cool`: Kính râm ngầu lòi, Slot=Glasses, File=Assets/Clothing/Glasses/sunglasses.png, 64x64, RGBA, 96.1% transparent
6. `ribbon_pink`: Nơ hồng kute, Slot=Bow, File=Assets/Clothing/Bows/pink_bow.png, 64x64, RGBA, 97.4% transparent
7. `backpack_cute`: Balo phượt nhỏ, Slot=Backpack, File=Assets/Clothing/Backpacks/small_backpack.png, 64x64, RGBA, 96.2% transparent

- **Kiểm tra tệp đầu ra**:
  - `bin/Debug/net8.0-windows`: Đầy đủ 7/7 assets
  - `bin/Release/net8.0-windows`: Đầy đủ 7/7 assets
  - `Publish/DesktopPetWorld`: Đầy đủ 7/7 assets
  - Bộ cài đặt Inno Setup `Installer/DesktopPetWorld-Setup.exe`: Đóng gói thành công 100%.

---

## 5. KẾT QUẢ KIỂM THỬ HỆ THỐNG (TEST RESULTS)

Bộ kiểm thử `scratch/OutfitTests.csproj`:
**223 PASSED, 0 FAILED**

1. Chuẩn hóa 4 slot độc lập: Hat, Glasses, Bow, Backpack.
2. Quy tắc thay thế: Cùng slot tự thay thế; khác slot trang bị đồng thời.
3. Cô lập hoàn toàn Food & Toy không thể gắn vào slot phụ kiện.
4. Giữ nguyên frame khi trang bị phụ kiện hoặc đổi hướng giữa chừng (không reset về frame 0).
5. Đồng bộ hoàn hảo trên 4 mức PetScale: 0.75, 1.0, 1.25, 1.5.

---

## 6. HẠN CHẾ CÒN LẠI & HƯỚNG MỞ RỘNG (LIMITATIONS & RECOMMENDATIONS)
- Cả 7 phụ kiện hiện tại là ảnh tĩnh đơn 64x64 được định vị giải phẫu theo từng frame bằng ma trận `GetFrameTransforms`.
- Nếu sau này thêm các sprite strip hoạt họa (ví dụ nơ bay theo gió, đuôi mũ vẫy), hệ thống `MapFrameIndex` đã sẵn sàng hỗ trợ tự động crop frame tương ứng mà không cần sửa đổi kiến trúc.
