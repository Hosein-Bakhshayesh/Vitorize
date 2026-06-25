namespace Vitorize.Web.Models.Admin.Common
{
    public class AdminPagedResultModel<T>
    {
        public List<T> Items { get; set; } = new();
        public List<T> Data { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages { get; set; }

        public List<T> Rows => Items.Any() ? Items : Data;
    }
}
