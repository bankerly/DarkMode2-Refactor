namespace DarkMode.Core.Interfaces.Hardware;

/**
 * 硬件
 */
public interface IHardwareService
{
    // 获取 GPU 使用率
    float GetGpuUsage();
    // 获取环境光传感器数据（单位 lx；设备没有环境光传感器或读取失败时返回 null）
    float? GetAmbientLight();
}