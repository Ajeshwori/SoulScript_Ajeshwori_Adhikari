using Microsoft.EntityFrameworkCore;
using SoulScript.Model;

namespace SoulScript.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // Seed moods (only if empty)
        if (!await db.Moods.AnyAsync())
        {
            var moods = new List<Mood>
            {
                // Positive
                new() { MoodName="Happy",     Category=MoodCategory.Positive, EmojiIcon="😊" },
                new() { MoodName="Excited",   Category=MoodCategory.Positive, EmojiIcon="🤩" },
                new() { MoodName="Relaxed",   Category=MoodCategory.Positive, EmojiIcon="😌" },
                new() { MoodName="Grateful",  Category=MoodCategory.Positive, EmojiIcon="🙏" },
                new() { MoodName="Confident", Category=MoodCategory.Positive, EmojiIcon="😎" },

                // Neutral
                new() { MoodName="Calm",       Category=MoodCategory.Neutral, EmojiIcon="😌" },
                new() { MoodName="Thoughtful", Category=MoodCategory.Neutral, EmojiIcon="🤔" },
                new() { MoodName="Curious",    Category=MoodCategory.Neutral, EmojiIcon="🧐" },
                new() { MoodName="Nostalgic",  Category=MoodCategory.Neutral, EmojiIcon="🥲" },
                new() { MoodName="Bored",      Category=MoodCategory.Neutral, EmojiIcon="😐" },

                // Negative
                new() { MoodName="Sad",     Category=MoodCategory.Negative, EmojiIcon="😢" },
                new() { MoodName="Angry",   Category=MoodCategory.Negative, EmojiIcon="😠" },
                new() { MoodName="Stressed",Category=MoodCategory.Negative, EmojiIcon="😣" },
                new() { MoodName="Lonely",  Category=MoodCategory.Negative, EmojiIcon="😔" },
                new() { MoodName="Anxious", Category=MoodCategory.Negative, EmojiIcon="😰" },
            };

            db.Moods.AddRange(moods);
        }

        // Seed pre-built tags (only if empty)
        if (!await db.Tags.AnyAsync(t => t.IsPrebuilt))
        {
            var now = DateTime.Now;

            string[] prebuilt =
            {
                "Work","Career","Studies","Family","Friends","Relationships","Health","Fitness",
                "Personal Growth","Self-care","Hobbies","Travel","Nature","Finance","Spirituality",
                "Birthday","Holiday","Vacation","Celebration","Exercise","Reading","Writing","Cooking",
                "Meditation","Yoga","Music","Shopping","Parenting","Projects","Planning","Reflection"
            };

            db.Tags.AddRange(prebuilt.Select(name => new Tag
            {
                TagName = name,
                IsPrebuilt = true,
                CreatedAt = now,
                UsageCount = 0
            }));
        }

        await db.SaveChangesAsync();
    }
}
