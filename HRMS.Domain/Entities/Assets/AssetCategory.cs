using System.Collections.Generic;
using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Assets
{
    /// <summary>
    /// Category grouping for assets (e.g. Laptops, Mobile Phones, Furniture).
    /// </summary>
    public class AssetCategory : ICompanyOwned
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int CompanyId { get; set; }
        int? ICompanyOwned.CompanyId => CompanyId;

        public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}
