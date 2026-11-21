# Sao lưu và quay lại bản cũ

Chép `rollback.py` vào `/home/pi/`. Đóng CurtantControl và app Python cũ trước
khi chạy. File dùng Python3 có sẵn trên Pi, tự xin sudo khi cần.

**Trước khi cài bản final mới:**

```sh
python3 /home/pi/rollback.py backup
```

Lệnh in đường dẫn `/home/pi/CurtantControl-Rollback-<thời gian>.tar.gz`.
Giữ file này cùng `rollback.py`; nên chép thêm ra USB. Chỉ cài bản mới sau khi
có thông báo `Backup saved`. Có thể kiểm tra bằng:

```sh
python3 /home/pi/rollback.py check /home/pi/CurtantControl-Rollback-<thời gian>.tar.gz
```

**Khi muốn quay lại:** đóng app, thay tên file dưới đây bằng đúng tên đã sao lưu:

```sh
python3 /home/pi/rollback.py restore /home/pi/CurtantControl-Rollback-<thời gian>.tar.gz
sudo reboot
```

Khôi phục toàn bộ `/home/pi/r1` (gồm key_check và app Python cũ), app CurtantControl,
JSON cấu hình/trạng thái, autostart, cấu hình LXDE-pi, shortcut chuẩn, service
keycheck và `/etc/rc.local`. Startup trở về đúng thời điểm sao lưu; không tự bật
app Python cũ nếu tại thời điểm sao lưu nó đã bị tắt khỏi startup.

Trước khi khôi phục, công cụ tạo thêm `CurtantControl-Before-Restore-*.tar.gz`
và chuyển các file/thư mục bị thay thế sang tên có `.before-rollback-*`.
Không tự xóa bản sao lưu. Không khôi phục OS, gói apt, firmware hay boot/EEPROM.
Chỉ dùng bản sao lưu của chính máy Pi này; bản sao lưu có thể chứa thông tin riêng.

Nếu đã cài bản mới trước khi tạo snapshot, công cụ chỉ sao lưu bản hiện đang có.
Không tự ghép các `app-backup-*` và keycheck `.bak-*` khác thời điểm thành bản cũ.

Đã kiểm tra vòng sao lưu/khôi phục bằng thư mục thử nghiệm và Python3.7 syntax;
chưa chạy systemd/sudo hoặc khôi phục trên Pi thật.


## Chuyển về Chromium cũ ngay (từ0.9.3)

Bundle `rollback.sh` dùng chế độ này, tự dừng app hiện tại và khởi động hệ thống cũ:

```sh
python3 rollback.py legacy
```

Có thể thêm đường dẫn snapshot sau `legacy` để khôi phục file trước khi bật lại.
Cần giữ key_check1.py, model_1/app_model_1.py và launcher cr_*.sh gốc trong r1
hoặc snapshot. Công cụ không tái tạo file Python cũ nếu chúng đã bị xóa.
Lệnh `restore` ở trên chỉ khôi phục snapshot; `legacy` còn chuyển startup/service
và mở Chromium ngay, không cần reboot để chuyển giao diện.
