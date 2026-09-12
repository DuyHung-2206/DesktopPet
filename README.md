# 🎮 DESKTOP PET WORLD – GAME NUÔI THÚ ẢO TRÊN MÀN HÌNH WINDOWS

Chào mừng bạn đến với **Desktop Pet World** – Trò chơi nuôi thú ảo tương tác trực tiếp trên màn hình Desktop Windows! Game mang phong cách dễ thương (Cute + Funny + Cartoon), chạy độc lập, hoàn toàn **Offline**, không khung viền, nền trong suốt và tự động tương tác với người dùng.

---

## 📑 MỤC LỤC
1. [Yêu Cầu Hệ Thống](#1-yêu-cầu-hệ-thống)
2. [Cấu Trúc Thư Mục Dự Án](#2-cấu-trúc-thư-mục-dự-án)
3. [Cách Mở Project (Visual Studio / Rider / VS Code)](#3-cách-mở-project)
4. [Cách Chạy Thử Nghiệm (Debug)](#4-cách-chạy-thử-nghiệm-debug)
5. [Cách Build Bản Release Độc Lập (Self-Contained EXE)](#5-cách-build-bản-release-độc-lập)
6. [Cách Tạo Bản Portable Không Cần Cài Đặt](#6-cách-tạo-bản-portable-không-cần-cài-đặt)
7. [Cách Tạo Bộ Cài Đặt Windows (Installer Setup.exe)](#7-cách-tạo-bộ-cài-đặt-windows)
8. [Vị Trí Save Game & Cơ Chế Offline Time](#8-vị-trí-save-game--cơ-chế-offline-time)
9. [Hướng Dẫn Mở Rộng: Thêm Loài Pet Mới](#9-hướng-dẫn-mở-rộng-thêm-loài-pet-mới)
10. [Hướng Dẫn Mở Rộng: Thêm Vật Phẩm Mới (Item/Food/Toy/Clothes)](#10-hướng-dẫn-mở-rộng-thêm-vật-phẩm-mới)
11. [Hướng Dẫn Mở Rộng: Thêm Hoạt Ảnh & Sprite PNG](#11-hướng-dẫn-mở-rộng-thêm-hoạt-ảnh--sprite-png)
12. [Xử Lý Sự Cố Thường Gặp (Troubleshooting)](#12-xử-lý-sự-cố-thường-gặp)

---

## 1. Yêu Cầu Hệ Thống

- **Hệ điều hành**: Windows 10 (1809 trở lên) hoặc Windows 11 (64-bit).
- **Phần cứng**:
  - CPU: 1.0 GHz hoặc nhanh hơn.
  - RAM: Tối thiểu 512 MB khả dụng.
  - Dung lượng trống: 150 MB.
- **Đối với người dùng cuối (Bản Release / Portable / Installer)**:
  - **KHÔNG** yêu cầu cài Visual Studio.
  - **KHÔNG** yêu cầu cài .NET Runtime (Đã tích hợp sẵn theo dạng Self-Contained win-x64).
- **Đối với lập trình viên (Muốn sửa mã nguồn)**:
  - .NET 8.0 SDK (hoặc mới hơn).
  - Visual Studio 2022 (với Workload *.NET Desktop Development*) hoặc JetBrains Rider / VS Code (C# Dev Kit).

---

## 2. Cấu Trúc Thư Mục Dự Án

```
DesktopPet/
│
├── Assets/                        # Tài nguyên đồ họa & âm thanh
│   ├── Pets/                      # Thư mục chứa sprite cho từng loài thú cưng
│   │   ├── Cat/
│   │   ├── Dog/
│   │   ├── Rabbit/
│   │   ├── Fox/
│   │   ├── Panda/
│   │   └── Dragon/
│   ├── Food/                      # Hình ảnh thức ăn
│   ├── Clothes/                   # Hình ảnh trang phục
│   ├── Toys/                      # Hình ảnh đồ chơi
│   ├── Audio/                     # Hiệu ứng âm thanh .WAV
│   └── Icons/                     # Icon ứng dụng (app.ico)
│
├── Models/                        # Data Entities & Enums
│   ├── Pet.cs                     # Model thú cưng (Stats, Level, Exp, Vị trí...)
│   ├── PetSpecies.cs              # Định nghĩa loài thú cưng (Data-driven)
│   ├── PetState.cs                # Enum trạng thái: Idle, Walk, Sleep, Fall...
│   ├── Item.cs                    # Định nghĩa vật phẩm (Food, Toy, Accessory)
│   ├── InventoryItem.cs           # Vật phẩm trong túi & số lượng
│   ├── GameSave.cs                # Cấu trúc lưu trữ tiến trình game
│   ├── Achievement.cs             # Thành tựu & danh hiệu
│   └── DailyReward.cs             # Điểm danh 7 ngày
│
├── ViewModels/                    # Kiến trúc MVVM
│   ├── ViewModelBase.cs           # INotifyPropertyChanged & RelayCommand
│   ├── PetViewModel.cs            # Điều phối hành vi, chỉ số, vòng lặp AI
│   ├── ShopViewModel.cs           # Mua sắm vật phẩm & mở khóa pet
│   ├── InventoryViewModel.cs      # Quản lý túi đồ, mặc trang phục
│   ├── PetCollectionViewModel.cs  # Quản lý bộ sưu tập & chọn pet
│   ├── SettingsViewModel.cs       # Cài đặt âm thanh, tỉ lệ, màn hình
│   └── MainDashboardViewModel.cs  # Bảng điều khiển trung tâm & điểm danh
│
├── Views/                         # Giao diện XAML WPF
│   ├── PetWindow.xaml             # Cửa sổ chính trong suốt nổi trên Desktop
│   ├── Controls/
│   │   ├── PetRenderer.xaml       # Bộ render vector procedural & sprite fallback
│   │   ├── EmoteBubble.xaml       # Bóng thoại cảm xúc trôi bồng bềnh
│   │   └── PetStatusPopup.xaml    # Bảng hiển thị mini chỉ số khi click pet
│   ├── ShopWindow.xaml            # Cửa sổ Shop mua sắm
│   ├── InventoryWindow.xaml       # Cửa sổ túi đồ
│   ├── PetSelectionWindow.xaml    # Cửa sổ bộ sưu tập pet
│   ├── SettingsWindow.xaml        # Cửa sổ cài đặt
│   ├── MainDashboardWindow.xaml   # Bảng điều khiển quản lý game
│   └── MiniGameBallWindow.xaml    # Mini Game bắt bóng trên màn hình
│
├── Services/                      # Các dịch vụ xử lý nền tảng
│   ├── PetAIService.cs            # Finite State Machine điều khiển hành vi & vật lý rơi
│   ├── PetStatService.cs          # Quản lý suy giảm & hồi phục 6 chỉ số
│   ├── OfflineTimeService.cs      # Tính toán thời gian trôi qua khi tắt game
│   ├── SaveService.cs             # Đọc/ghi JSON an toàn kèm backup tự động
│   ├── AudioService.cs            # Phát âm thanh đa âm sắc & synthesizer
│   ├── ScreenService.cs           # Nhận diện đa màn hình & giới hạn di chuyển
│   ├── NotificationService.cs     # Quản lý thông báo nhắc nhở chống spam
│   ├── DataManager.cs             # Nạp dữ liệu JSON cấu hình
│   ├── LoggerService.cs           # Ghi log chuẩn vào file game.log
│   └── TrayService.cs             # Điều khiển khay hệ thống Windows (System Tray)
│
├── Data/                          # Dữ liệu JSON cấu hình (Data-Driven)
│   ├── pets.json                  # Thông số các loài thú cưng
│   ├── items.json                 # Danh sách thức ăn, đồ chơi, trang phục
│   └── achievements.json          # Danh sách thành tựu & phần thưởng
│
├── App.xaml / App.xaml.cs         # Khởi động, Single-Instance, Exception Handler
├── build.bat                      # Script biên dịch tự động 1-click
├── installer.iss                  # Kịch bản đóng gói Inno Setup
├── DesktopPet.csproj              # Project file .NET 8 WPF
└── README.md                      # Hướng dẫn chi tiết
```

---

## 3. Cách Mở Project

### Với Visual Studio 2022:
1. Mở Visual Studio 2022.
2. Chọn **Open a project or solution**.
3. Duyệt đến thư mục `DesktopPet` và chọn file `DesktopPet.csproj`.
4. Visual Studio sẽ tự động nạp project và restore các dependency.

### Với JetBrains Rider:
1. Mở Rider.
2. Chọn **Open** và chọn file `DesktopPet.csproj`.

### Với Visual Studio Code:
1. Mở VS Code trong thư mục dự án.
2. Đảm bảo đã cài extension **C#** (hoặc **C# Dev Kit**).
3. Mở Terminal tích hợp trong VS Code.

---

## 4. Cách Chạy Thử Nghiệm (Debug)

Mở PowerShell hoặc Command Prompt tại thư mục dự án:
```powershell
dotnet run
```
Hoặc trong Visual Studio / Rider, nhấn phím **F5** (Debug) hoặc **Ctrl + F5** (Run without debugging).

> [!TIP]
> Khi game khởi động, chú pet sẽ xuất hiện ở góc dưới màn hình Desktop. Bạn có thể kéo thả chú pet bằng chuột trái hoặc nhấn chuột phải để mở menu tương tác!

---

## 5. Cách Build Bản Release Độc Lập

Dự án có sẵn script tự động `build.bat`:
1. Nhấp đúp chuột vào file **`build.bat`**.
2. Script sẽ tự động:
   - Dọn dẹp thư mục build cũ.
   - Restore các thư viện.
   - Biên dịch chế độ `Release`.
   - Đóng gói **Self-Contained win-x64** (chạy độc lập, không cần cài .NET Runtime trên máy khác).
   - Tự động sao chép thư mục `Assets/` và `Data/`.
3. Kết quả xuất hiện tại:
   `Build\Release\win-x64\DesktopPet.exe`
   hoặc thư mục phân phối:
   `Publish\DesktopPetWorld\`

Bạn cũng có thể chạy lệnh CLI tương đương:
```powershell
dotnet publish DesktopPet.csproj -c Release -r win-x64 --self-contained true -o ./Publish/DesktopPetWorld
```

---

## 6. Cách Tạo Bản Portable Không Cần Cài Đặt

Sau khi chạy `build.bat` (hoặc lệnh publish):
1. Thư mục `Publish\DesktopPetWorld` đã chứa đầy đủ file `DesktopPet.exe`, các thư viện `.dll`, thư mục `Data/` và `Assets/`.
2. Nén toàn bộ thư mục `Publish\DesktopPetWorld` thành file `.zip` (ví dụ: `DesktopPetWorld-Portable.zip`).
3. Gửi file zip này sang bất kỳ máy tính Windows 10/11 64-bit nào, giải nén và nhấp đúp vào `DesktopPet.exe` là chơi được ngay lập tức!

---

## 7. Cách Tạo Bộ Cài Đặt Windows (Installer Setup.exe)

Dự án đã chuẩn bị sẵn kịch bản Inno Setup: **`installer.iss`**.
1. Cài đặt phần mềm miễn phí [Inno Setup](https://jrsoftware.org/isdl.php) (phiên bản 6.x trở lên).
2. Chạy `build.bat` để đảm bảo thư mục `Publish\DesktopPetWorld` đã có bản build mới nhất.
3. Nhấp chuột phải vào file `installer.iss` chọn **Compile**.
4. Inno Setup sẽ tạo ra file cài đặt:
   `Output\DesktopPetWorld-Setup.exe`
5. File cài đặt này sẽ:
   - Tự động tạo thư mục cài đặt trong `Program Files` hoặc thư mục người dùng.
   - Tạo shortcut trên Desktop và trong Menu Start.
   - Cung cấp tính năng gỡ cài đặt (Uninstall) sạch sẽ thông qua Windows Settings.

---

## 8. Vị Trí Save Game & Cơ Chế Offline Time

### Vị trí lưu trữ:
Để đảm bảo ứng dụng không bao giờ bị lỗi quyền ghi file (Permission Denied) khi cài vào `Program Files`, dữ liệu lưu trữ được đặt tại thư mục cá nhân người dùng:
```
%AppData%\DesktopPetWorld\savegame.json
```
Thư mục nhật ký (Logs):
```
%AppData%\DesktopPetWorld\logs\game.log
```

### Cơ chế tự động sao lưu:
- Trước mỗi lần lưu dữ liệu mới, file cũ được tự động sao lưu thành `savegame.json.bak`.
- Nếu file chính bị lỗi cấu trúc (ví dụ: máy tính sập nguồn đột ngột), game sẽ tự động khôi phục từ file backup mà không làm mất tiến trình của người chơi.

### Cơ chế Offline Time (Thời gian ngoại tuyến):
- Khi bạn tắt game, game ghi nhận thời điểm thoát (`LastPlayedUtc`).
- Khi bạn mở lại game, hệ thống tính toán thời gian đã trôi qua:
  - Thú cưng được nghỉ ngơi hồi phục Năng Lượng (⚡ Energy).
  - Độ đói giảm dần (🍖 Hunger).
  - Giới hạn tính toán tối đa 24 giờ để đảm bảo thú cưng không bao giờ bị đói kiệt sức đến chết nếu bạn vắng mặt nhiều ngày.

---

## 9. Hướng Dẫn Mở Rộng: Thêm Loài Pet Mới

Game được thiết kế theo mô hình **Data-Driven**, bạn có thể thêm loài thú cưng mới mà không cần biên dịch lại mã nguồn!

Mở file `Data/pets.json` và thêm một đối tượng mới:
```json
{
  "id": "fox",
  "name": "Fox",
  "displayName": "Cáo Foxy 🦊",
  "description": "Thông minh, lanh lợi với bộ lông cam rực rỡ.",
  "hungerRate": 1.1,
  "happinessRate": 1.2,
  "energyRate": 1.1,
  "cleanlinessRate": 1.0,
  "speed": 3.0,
  "favoriteFood": "chicken",
  "favoriteToy": "yoyo",
  "unlockPrice": 250,
  "isUnlocked": false,
  "baseColor": "#FF7043",
  "secondaryColor": "#FFFFFF"
}
```
Khởi động lại game, loài mới sẽ tự động xuất hiện trong **Bộ Sưu Tập** và **Cửa Hàng**!

---

## 10. Hướng Dẫn Mở Rộng: Thêm Vật Phẩm Mới

Mở file `Data/items.json` và thêm vật phẩm tùy ý:

### Ví dụ thêm Thức ăn mới:
```json
{
  "id": "pizza",
  "name": "Pizza phô mai béo ngậy",
  "category": "Food",
  "icon": "🍕",
  "description": "Món ăn tiệc tùng thơm nức mũi, tăng nhiều niềm vui.",
  "price": 45,
  "hungerRestore": 50,
  "happinessBonus": 35,
  "energyBonus": 15,
  "cleanlinessImpact": -5
}
```

### Ví dụ thêm Trang phục mới:
```json
{
  "id": "party_hat",
  "name": "Mũ sinh nhật chóp nhọn",
  "category": "Accessory",
  "slot": "Head",
  "icon": "🎉",
  "description": "Chiếc mũ tiệc tùng lấp lánh.",
  "price": 95,
  "hungerRestore": 0,
  "happinessBonus": 40,
  "energyBonus": 0,
  "cleanlinessImpact": 0
}
```

---

## 11. Hướng Dẫn Mở Rộng: Thêm Hoạt Ảnh & Sprite PNG

Game hỗ trợ cơ chế **Hybrid Rendering**:
- Mặc định: Tự động vẽ hoạt ảnh vector mượt mà bằng WPF.
- Nếu bạn có ảnh Sprite 2D (PNG) tự vẽ:
  1. Đặt ảnh vào thư mục: `Assets/Pets/[Loài]/[TrạngThái].png`
  2. Ví dụ:
     - `Assets/Pets/Cat/Idle.png`
     - `Assets/Pets/Cat/Walk.png`
     - `Assets/Pets/Cat/Sleep.png`
     - `Assets/Pets/Cat/Eat.png`
  3. Game sẽ tự động nhận diện file ảnh và ưu tiên hiển thị thay thế đồ họa vector!

---

## 12. Xử Lý Sự Cố Thường Gặp

### Q1: Nhấp đúp vào file exe nhưng không thấy gì hiện lên?
- **Nguyên nhân**: Game đã đang chạy ẩn ở khay hệ thống (System Tray).
- **Cách khắc phục**: Nhìn xuống góc dưới cùng bên phải màn hình (gần đồng hồ Windows), nhấn vào icon mũi tên `^` của System Tray, nhấp đúp vào icon chú cún cưng để đưa Pet lên màn hình.

### Q2: Pet bị che khuất sau thanh Taskbar?
- Game tự động nhận diện vùng làm việc (Working Area) không che thanh Taskbar. Nếu bạn đổi độ phân giải màn hình trong khi chơi, chỉ cần nhấc chú pet lên cao một chút bằng chuột trái rồi thả ra, pet sẽ tự rơi xuống mặt sàn ảo một cách chính xác.

### Q3: Muốn chơi trên màn hình phụ (Màn hình thứ 2)?
- Nhấp chuột phải vào pet -> Chọn **⚙️ Cài Đặt** -> Tại mục **Chọn màn hình**, chọn Màn hình 2 -> Nhấn **LƯU CÀI ĐẶT**.

### Q4: Muốn đặt lại dữ liệu chơi từ đầu (Reset)?
- Nhấn tổ hợp phím `Windows + R`, gõ `%AppData%\DesktopPetWorld` và nhấn Enter.
- Xóa file `savegame.json` và mở lại game.

---

Chúc bạn có những giờ phút thư giãn tuyệt vời cùng những người bạn nhỏ trong **Desktop Pet World**! 🐾💖
