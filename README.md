## 简介

使用 `ffmpeg` 将视频转换成手机能播放的 `mp4` 格式

### 已知 BUG

- 点击 `remove` 时会错误删除其他列表项
- 中文显示成方框(不影响使用)
- 左上角的尺寸设置无法使用

<del>这些都是上游依赖的bug，修是修不来的，只能凑合着用这样子的啦</del>

等待上游依赖修复bug中

### 功能介绍

有如下功能

- 对图片进行裁剪
- 将其他视频格式转换到 mp4

### 运行

依赖:
- `dotnet core` `3.1` 及以上的版本
- 需要下载好对应平台的 `ffmpeg` 可执行文件放到 `ffmpeg-bin/win-x64/ffmpeg` 位置

```sh
git clone https://github.com/shynome/TFFmpeg.git
cd TFFmpeg
dotnet run
# 打包可执行文件
dotnet publish -r win-x64 -c Release
```
