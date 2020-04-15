using Insurance.Services;
using VirtualCredit.Services;

namespace VirtualCredit.Models
{
    public class ViewModelBase
    {
        [DatabaseProp] public string Name { get; set; }
        [DatabaseProp] public string Telephone { get; set; }
        [DatabaseProp] public string Mail { get; set; }
        [DatabaseProp] public string CompanyName { get; set; }
        [DatabaseProp] public string CompanyNameAbb { get; set; }
        [DatabaseProp] public string RecipeCompany { get; set; }
        [DatabaseProp] public string TaxNum { get; set; }
        [DatabaseProp] public string RecipeBank { get; set; }
        [DatabaseProp] public string RecipeAccount { get; set; }
        [DatabaseProp] public string RecipeAddress { get; set; }
        [DatabaseProp] public string RecipePhone { get; set; }
        [DatabaseProp] public string RecipeType { get; set; }
        public string UserNameEdit { get; set; }
        public ReaderWriterLockerWithName MyLocker { get; set; }
    }
}
