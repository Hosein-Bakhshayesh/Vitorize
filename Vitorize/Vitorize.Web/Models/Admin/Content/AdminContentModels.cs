using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Content
{
    public class AdminPageListItemModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public bool IsPublished { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminPageModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ContentHtml { get; set; } = string.Empty;
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public bool IsSystem { get; set; }
        public bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminPageInputModel
    {
        public Guid? Id { get; set; }
        public bool IsSystem { get; set; }

        [Required(ErrorMessage = "عنوان صفحه الزامی است.")]
        [StringLength(250, ErrorMessage = "عنوان صفحه نمی‌تواند بیشتر از ۲۵۰ نویسه باشد.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "نشانی صفحه (Slug) الزامی است.")]
        [StringLength(250, ErrorMessage = "نشانی صفحه نمی‌تواند بیشتر از ۲۵۰ نویسه باشد.")]
        public string Slug { get; set; } = string.Empty;

        public string? ContentHtml { get; set; }

        [StringLength(250, ErrorMessage = "عنوان سئو نمی‌تواند بیشتر از ۲۵۰ نویسه باشد.")]
        public string? SeoTitle { get; set; }

        [StringLength(500, ErrorMessage = "توضیحات سئو نمی‌تواند بیشتر از ۵۰۰ نویسه باشد.")]
        public string? SeoDescription { get; set; }

        public bool IsPublished { get; set; }
    }

    public class AdminFaqModel
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminFaqInputModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "متن پرسش الزامی است.")]
        [StringLength(500, ErrorMessage = "پرسش نمی‌تواند بیشتر از ۵۰۰ نویسه باشد.")]
        public string Question { get; set; } = string.Empty;

        [Required(ErrorMessage = "متن پاسخ الزامی است.")]
        [StringLength(4000, ErrorMessage = "پاسخ نمی‌تواند بیشتر از ۴۰۰۰ نویسه باشد.")]
        public string Answer { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "ترتیب نمایش نمی‌تواند منفی باشد.")]
        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
