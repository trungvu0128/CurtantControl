# Cai dat CurtantControl tren Raspberry Pi

1. Chep file .tar.gz sang Pi va giai nen.
2. Mo Terminal trong thu muc vua giai nen.
3. Chay `sh install.sh` bang tai khoan desktop, KHONG chay ca bo cai bang sudo.
4. Nhap mat khau sudo khi bo cai yeu cau de cai thu vien Linux.
5. Mo shortcut CurtantControl tren desktop. Neu vua them nhom gpio/video, dang xuat va dang nhap lai.

Can Internet de cai FFmpeg va cac goi he dieu hanh. Ung dung, .NET runtime va18
anh mau da nam trong bundle; khong can cai .NET SDK. Bo cai tu chon Buster32-bit
hoac Raspberry Pi OS12/13 32/64-bit. Can desktop X11/XWayland; khong phai OS Lite.

Duong dan ung dung voi tai khoan pi: `/home/pi/.local/share/CurtantControl/app/`.
Cau hinh GPIO: `photobooth.json`; mau/thu tu lop: `colors.json` trong thu muc nay.
Ban0.7.4 dung profile ban da cung cap: SimulateRelays=false, ActiveLow=false,
BCM [22,10,9,11,5,6,13,19], camera http://127.0.0.1:5000/video_feed.
Thu tu mac dinh R1/R2 gan camera nhat. Khi cap nhat, JSON cu van duoc giu nguyen.

Cap nhat: dong ung dung, chay `sh install.sh` trong bundle moi. Bo cai giu nguyen
photobooth.json va colors.json da co, dong thoi tao thu muc app-backup-TIMESTAMP.

Ung dung mo User mode toan man hinh. Giu goc duoi ben phai3 giay de chon Developer.
Chon mau de xem anh mau, bam START moi gui lenh relay. Nam nut xep gan camera den
xa, nen fixed cuoi cung. Developer > Settings cho chon R1/R2 hoac R7/R8 gan camera
nhat; Save khong lam dong co chay. Trang thai hien thi la uoc luong theo lenh,
khong phai cam bien vi tri. Ban Buster la ban tuong thich thu nghiem.

Kiem tra truoc khi cai: `sh install.sh --check`.
Neu da du thu vien: `sh install.sh --skip-packages`.
Chan doan: `sh ~/.local/share/CurtantControl/app/diagnose.sh`.
