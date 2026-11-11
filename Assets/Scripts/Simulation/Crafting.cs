using System;
using System.Collections.Generic;

namespace Factory.Simulation
{
    /// <summary>
    /// One crafting-table recipe: consume Ingredients, get Result. Id is stable (commands and
    /// saves refer to it), so never renumber a recipe; add new ones with new Ids.
    /// Each ingredient item appears at most once per recipe.
    /// </summary>
    public sealed class CraftingRecipe
    {
        public readonly int Id;
        public readonly ItemAmount Result;
        public readonly ItemAmount[] Ingredients;

        public CraftingRecipe(int id, ItemAmount result, params ItemAmount[] ingredients)
        {
            Id = id;
            Result = result;
            Ingredients = ingredients ?? Array.Empty<ItemAmount>();
        }
    }

    /// <summary>Read-only questions about recipes against an inventory, for rules and the window.</summary>
    public static class Crafting
    {
        /// <summary>How many times the recipe can be crafted with what is in inv right now.</summary>
        public static int MaxCraftable(CraftingRecipe recipe, Inventory inv)
        {
            int max = int.MaxValue;
            foreach (ItemAmount ing in recipe.Ingredients)
                if (ing.Count > 0) max = Math.Min(max, inv.Count(ing.Item) / ing.Count);
            return recipe.Ingredients.Length == 0 ? 0 : max;
        }

        public static bool CanCraft(CraftingRecipe recipe, Inventory inv) => MaxCraftable(recipe, inv) > 0;

        /// <summary>What is missing to craft the recipe once: each short ingredient and by how much.</summary>
        public static List<ItemAmount> Missing(CraftingRecipe recipe, Inventory inv)
        {
            var missing = new List<ItemAmount>();
            foreach (ItemAmount ing in recipe.Ingredients)
            {
                int have = inv.Count(ing.Item);
                if (have < ing.Count) missing.Add(new ItemAmount(ing.Item, ing.Count - have));
            }
            return missing;
        }
    }
}
