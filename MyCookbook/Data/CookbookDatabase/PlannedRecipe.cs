#nullable disable

using Microsoft.EntityFrameworkCore;

namespace MyCookbook.Data.CookbookDatabase
{
    [PrimaryKey("UserName", "Id")]
    public partial class PlannedRecipe
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public int RecipeId { get; set; }
        public DateOnly Date { get; set; }
        public bool FromFridge { get; set; }

        public virtual Recipe Recipe { get; set; }
    }
}