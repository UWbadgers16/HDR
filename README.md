# AdvancedHDR for Windows Phone 8.1
CS 766 HDR app

## Motivation
The AdvancedHDR project was started with the goal of bring a full High Dynamic Range implemented to the WinRT environment. Nokia, one of the world's leaders in digital imaging on smartphones, joined Microsoft several years ago to create Nokia-made Windows Phones. Nokia Pureview technology has brought many new enhancements to smartphone photography, such as the [Nokia Lumia 1020](http://www.microsoft.com/en-us/mobile/phone/lumia1020/), which brings a 41 MP sensor to a smartphone. With these considerations, and without a current implementation in the operating system, it was my goal to show that full HDR photography is fully realizable on a smartphone.

## AdvancedHDR app
With the goal of bringing full HDR imaging to a smartphone, the algorithm by [Debevic et al.](http://www.pauldebevec.com/Research/HDR/debevec-siggraph97.pdf) is implemented in the app. At a high level, the algorithm takes many photos at varying exposure times. Using the over and underexposed images, it is possible to map the small dynamic range of a smartphone into a larger range. In certain capacities, this can produce an image that's much closer to the range that humans see.
With the first implementation, AdvancedHDR is a bareboned app. The user is presented with a viewfinder and a single button to take a series of bracketed images at varying exposure rates. The rates are currently fixed. ![Screenshot](resources/screenshot.png)

### Details
The bracketed photos are taken at varying exposure values (EV). The base implementation doesn't have configurable exposure values, but the range extends from each device's minmum through its maximum exposure value. As 
