# Curtant Control {{VERSION}} — bản đầy đủ cho máy Pi hiện tại

Gồm toàn bộ app, runtime .NET8/linux-arm, ảnh nền, logo, key_check mới,
popup Starting/Stop căn giữa và bộ cài tự khởi động chung.
Chỉ dành cho Raspberry Pi4, Raspbian10 Buster32-bit, tài khoản desktop `pi`.
Đây là bản legacy tương thích thử nghiệm; chưa xác minh trên Pi thật.

## Cài mới hoặc cập nhật

1. Đóng CurtantControl đang chạy.
2. Giải nén toàn bộ gói, mở terminal trong thư mục vừa giải nén.
3. Chạy bằng tài khoản `pi`:

```sh
sh install.sh
```

Không dùng `sudo sh install.sh`. Bộ cài tự xin sudo cho thư viện OS và service.
Từ0.9.2, install.sh tự tạo và kiểm tra backup đầy đủ **trước mọi thay đổi**.
Từ0.9.4, cài lại sau rollback sẽ tự dừng keycheck/backend/app cũ trước khi chụp
backup; sau đó tắt startup của hệ thống cũ để không chạy song song với app mới.
Nếu backup lỗi thì dừng cài. Chạy lại bộ cài trong cùng thư mục sẽ giữ backup đầu
tiên, không ghi đè bằng trạng thái cài dở. Cần chỗ trống cho app và toàn bộ r1.
Cần Internet để cài các gói hệ thống còn thiếu. Runtime app đã có sẵn trong gói.
Sau khi cài xong, khởi động lại Pi để kiểm tra luồng tự mở app và popup Starting.

App nằm tại `/home/pi/.local/share/CurtantControl/app/`.
Keycheck nằm tại `/home/pi/r1/key_check_new.py`, chạy bằng `keycheck.service`.
File `key_check1.py` cũ được giữ lại; service dùng file mới.
Nếu máy chưa có service, bộ cài tạo mới. Shortcut/menu và autostart dùng chung
luồng mở app với popup, không tự mở thêm bản trùng.

## Cấu hình và bản sao lưu

Cập nhật giữ nguyên photobooth.json, colors.json, razr2-options.json hiện có.
Cấu hình và trạng thái người dùng trong `~/.local/share/CurtantControl/` được giữ
nguyên, gồm razr.json, camera-button-selection.json và trạng thái các rèm.
App cũ được giữ tại `app-backup-*`; keycheck/drop-in/shortcut đổi được sao lưu.
Không tự xóa các bản backup.

Cài mới dùng profile thiết bị đã cung cấp: R1–R8 là BCM
22,10,9,11,5,6,13,19; ActiveLow=false; xung500ms; tối đa1000ms; nghỉ100ms.
Cấu hình trên máy đang dùng được ưu tiên, không bị profile mới ghi đè.
Motor RazrII mặc định giả lập khi chưa có cấu hình riêng; máy cũ giữ thiết lập cũ.
Hai cơ cấu chỉ được điều khiển khi bấm START/STATE, không chạy khi Save hay mở app.
Từ0.9.5, chỉ giữ một shortcut CurtantControl trên Desktop. Bộ cài kiểm tra shortcut
hiện có trước khi tạo/cập nhật, chuyển icon trùng và bản sao .desktop.bak-* sang
~/.local/share/CurtantControl/shortcut-backups thay vì để trên Desktop.

## Chức năng nút

- GPIO24 / chân vật lý18: shutdown Pi an toàn. Chân này không bật lại được Pi4.
- GPIO25 / chân vật lý22: nếu app chưa chạy thì mở app và hiện Starting;
  nếu đang chạy thì hiện popup Stop, Cancel/Stop. Stop đóng app bình thường.
- Logo, chữ `Curtant Control` và nút popup được căn giữa. Kích thước nút giữ nguyên.

App gồm các thay đổi tới0.8.27: header tinh chỉnh5%, RazrI/II riêng biệt,
Motor/Relay/song song, lưu nút đã chọn qua khởi động lại, gửi góc rồi chờ Wait time
mà không kiểm tra phản hồi ổn định.

## Kiểm tra khi cần

```sh
sh install.sh --check
sudo systemctl status keycheck.service --no-pager -l
journalctl -u keycheck.service -n 50 --no-pager
```

Nếu thư viện OS đã đủ, có thể dùng `sh install.sh --skip-packages`.
Từ0.9.1 bộ cài tắt Chromium kiosk cũ của tài khoản pi và vô hiệu các dòng khởi
động kiosk đã nhận diện trong autostart, LXDE và rc.local, có backup trước khi sửa.
Không tắt cửa sổ browser thông thường. Không chạy thêm key_check1.py hoặc
app_model_1.py cũ cùng app mới vì có thể cùng dùng GPIO/I2C; bộ cài giữ nguyên
các dòng Python riêng này, chỉ tắt startup Chromium kiosk.

Gói này cần phiên desktop X11; không cài môi trường desktop hay firmware camera.
Các kiểm tra trên máy build không thay thế kiểm tra nút, GPIO, motor và camera trên Pi.

## Quay lại hệ thống Chromium cũ

Từ 0.11.3, sau khi cài thành công, bộ cài lưu lệnh rollback vào
`~/.local/share/CurtantControl/recovery/` rồi dọn các file cài đặt đã xác minh.
Dùng đường dẫn `rollback.sh` được in trong terminal. Các file backup vẫn nằm
trong `/home/pi/`. Nếu cài lỗi, giữ bộ cài để đọc log hoặc chạy lại.
Với bộ cài cũ còn nguyên thư mục, dùng `sh rollback.sh` trong bundle.
Có thể chỉ định đúng backup khi chạy từ thư mục chứa `rollback.sh`:

```sh
sh rollback.sh /home/pi/CurtantControl-Rollback-<thời gian>.tar.gz
```

Từ0.9.3, rollback.sh **dừng CurtantControl**, khôi phục snapshot nếu có, tắt
autostart app mới, chuyển keycheck.service về **key_check1.py gốc**, chạy lại
app_model_1.py bằng camrazr-legacy-backend.service và mở script Chromium cũ ngay.
Hệ thống cũ cũng tự chạy sau reboot. Nếu không có .rollback-target, dùng file cũ
còn trong /home/pi/r1. Công cụ kiểm tra key_check1.py, app_model_1.py và script
cr_*.sh/camrazr-client.sh trước khi dừng app; thiếu file thì báo lỗi và không đoán
nội dung chương trình cũ. Bản đang có được sao lưu thêm trước khi thay đổi.
Không gỡ gói apt, firmware hoặc hoàn tác nhóm người dùng.

Nếu chỉ cần khôi phục đúng snapshot mà không chuyển sang Chromium:

```sh
python3 rollback.py restore /home/pi/CurtantControl-Rollback-<thời gian>.tar.gz
sudo reboot
```


## PIN chuyển Camera Razr (từ0.10.0)

User mode ẩn Settings. Bấm Switch Razr sẽ hiện bàn phím số cảm ứng và yêu cầu PIN
mỗi lần chuyển I/II. PIN ban đầu **1234**. Đổi tại Developer mode → **Switch PIN**,
nhập 4–8 chữ số rồi nhập lại để xác nhận. File switch-pin.json nằm ngoài thư mục
app nên được giữ qua cập nhật và có trong backup. PIN chỉ khóa Switch Razr;
cách vào Developer mode bằng giữ góc dưới phải3giây vẫn giữ nguyên.


Từ0.10.1: bỏ nút Dark/Light trên cả hai màn Razr, luôn khởi động Dark mode
kể cả máy trước đó đã lưu Light mode.


Từ0.10.5: sửa Stop app chờ camera mãi. Nhả relay trước, giới hạn chờ camera3giây.
Nếu app vẫn chưa thoát, xem `tail -n 30 ~/.local/share/CurtantControl/shutdown.log`.


## Kiểm tra cập nhật (từ0.11.0)

Mỗi lần mở app, tự kiểm tra GitHub Releases trong nền. Có bản mới sẽ hiện thông
báo; không tự cài. Vào Developer mode → Updates → Download & install để tải,
kiểm tra checksum, backup và cập nhật. App chỉ đóng sau khi gói đã chuẩn bị xong;
cài xong tự mở lại. Với trình cập nhật mới, nếu phần hệ thống của bộ cài vẫn
khớp và dịch vụ nút bấm đã sẵn sàng, gói đầy đủ chỉ thay thư mục ứng dụng
bằng tài khoản pi. Không cần nhập mật khẩu. Nếu cần sửa phần hệ thống và máy
yêu cầu mật khẩu quản trị, Updates giữ app mở và giữ lại gói đã tải.
Nếu thiếu thư viện OS, dùng bộ cài đầy đủ bằng terminal. Cài bộ đầy đủ0.11.0 một
lần để có updater; bản small ba file trước đây không có helper tự cài.
Repo: https://github.com/trungvu0128/CurtantControl/releases
Log: journalctl --user -u 'curtantcontrol-update-*' --no-pager
