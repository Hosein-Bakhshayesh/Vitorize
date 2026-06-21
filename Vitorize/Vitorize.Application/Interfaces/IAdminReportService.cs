using Vitorize.Application.DTOs.Admin.Reports;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminReportService
    {
        Task<SalesReportDto> GetSalesReportAsync(ReportDateRangeDto filter);

        Task<PaymentsReportDto> GetPaymentsReportAsync(ReportDateRangeDto filter);

        Task<WalletReportDto> GetWalletReportAsync(ReportDateRangeDto filter);

        Task<CouponsReportDto> GetCouponsReportAsync(ReportDateRangeDto filter);

        Task<GiftCodesReportDto> GetGiftCodesReportAsync();

        Task<UsersReportDto> GetUsersReportAsync(ReportDateRangeDto filter);
    }
}