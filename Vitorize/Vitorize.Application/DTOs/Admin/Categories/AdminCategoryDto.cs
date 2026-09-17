namespace Vitorize.Application.DTOs.Admin.Categories
{
    public class AdminCategoryDto
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        public string? ImageAltText { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool ShowOnHome { get; set; } = true;
        public bool ShowInMenu { get; set; } = true;
        public int MenuSortOrder { get; set; }

        public bool IsActive { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public string? FocusKeyword { get; set; }

        /// <summary>Active products attached to this category itself, counted once even when a
        /// product reaches it through both its primary category and the many-to-many link.</summary>
        public int ProductCount { get; set; }

        /// <summary>Direct sub-categories that have not been deleted.</summary>
        public int ChildrenCount { get; set; }

        /// <summary>Active products anywhere under this category, including its own. This is the
        /// number that answers "will a customer landing here see anything?", so a category whose
        /// own count is zero but whose branches carry products is visibly different from an empty one.</summary>
        public int SubtreeProductCount { get; set; }
    }
}
