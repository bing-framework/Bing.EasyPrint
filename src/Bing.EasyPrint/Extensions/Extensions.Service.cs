using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint;

public static partial class Extensions
{
    /// <summary>
    /// 注册EasyPrint
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void AddEasyPrint(this IServiceCollection services)
    {
        services.AddScoped<IEasyPrint, DefaultEasyPrint>();
    }

    /// <summary>
    /// 注册EasyPrint
    /// </summary>
    /// <typeparam name="TEasyPrint">简单打印实现类型</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddEasyPrint<TEasyPrint>(this IServiceCollection services) where TEasyPrint : class, IEasyPrint
    {
        services.AddScoped<IEasyPrint, TEasyPrint>();
    }
}