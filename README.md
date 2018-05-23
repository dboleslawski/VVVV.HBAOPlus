HBAO+ is a high effiency screen-space ambient occlusion (SSAO) algorithm developed by NVIDIA.
This is a direct port for the vvvv dx11 pipeline by vux, made by exposing managed vvvv dx11 into native C++ dx11.

### features
* quite fast
* lots of pins (some hidden), exposes nearly all options of the NVIDIA library
* help patch, including explanations for every pin
* based on HBAO+ 4.0

### not done (yet?)
* including your own normal buffer (rendering AO on normal maps)
* dual layer depth input to remove halo artifacts

### installation
* make sure your dx11 pack is version >= 1.2
* drop it into your packs folder

### license
Wrapper & Nodes use the MIT license
HBAO+ is licensed via the __GameWorks Binary SDK EULA__  [https://developer.nvidia.com/gameworks-sdk-eula]

