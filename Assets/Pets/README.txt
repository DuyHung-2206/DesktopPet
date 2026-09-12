HƯỚNG DẪN THÊM TÀI NGUYÊN HÌNH ẢNH PET (SPRITE ASSETS)
======================================================
Hệ thống Desktop Pet World được thiết kế theo kiến trúc Hybrid:
1. Mặc định: Tự động vẽ hoạt ảnh vector procedural mượt mà không bao giờ lỗi.
2. Nâng cao: Nếu bạn muốn dùng ảnh riêng (Pixel Art hoặc 2D Sprite PNG), bạn chỉ cần đặt ảnh vào các thư mục tương ứng:
   - Assets/Pets/Cat/[Trạng thái].png
   - Assets/Pets/Dog/[Trạng thái].png
   - Assets/Pets/Rabbit/[Trạng thái].png
   - Assets/Pets/Fox/[Trạng thái].png
   - Assets/Pets/Panda/[Trạng thái].png
   - Assets/Pets/Dragon/[Trạng thái].png

Các trạng thái đặt tên theo enum PetState:
- Idle.png
- Walk.png
- Run.png
- Sleep.png
- Eat.png
- Happy.png
- Bath.png
- Sit.png
- Fall.png

Khi có file PNG trong thư mục, game sẽ tự động nhận diện và chuyển sang hiển thị ảnh của bạn!
