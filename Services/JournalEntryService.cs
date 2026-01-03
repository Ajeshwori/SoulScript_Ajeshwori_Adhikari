using System.Linq;
using Microsoft.EntityFrameworkCore;
using SoulScript.Data;
using SoulScript.Model;

namespace SoulScript.Services;

public class JournalEntryService
{
	private readonly ApplicationDbContext _db;
	public JournalEntryService(ApplicationDbContext db) => _db = db;

	public async Task<JournalEntry?> GetEntryByDateAsync(DateTime date)
	{
		return await _db.JournalEntries
			.Include(e => e.EntryTags).ThenInclude(et => et.Tag)
			.Include(e => e.EntryMoods).ThenInclude(em => em.Mood)
			.Include(e => e.Category)
			.FirstOrDefaultAsync(e => e.EntryDate == date.Date && !e.IsDeleted);
	}

	public Task SaveTodayAsync(
		string title,
		string content,
		string categoryName,
		List<string> tags,
		string primaryMoodName,
		List<string> secondaryMoodNames)
		=> SaveForDateAsync(DateTime.Today, title, content, categoryName, tags, primaryMoodName, secondaryMoodNames);

	public async Task SaveForDateAsync(
		DateTime date,
		string title,
		string content,
		string categoryName,
		List<string> tags,
		string primaryMoodName,
		List<string> secondaryMoodNames)
	{
		var targetDate = date.Date;
		var now = DateTime.Now;

		if (targetDate > DateTime.Today)
			throw new InvalidOperationException("Cannot save entries for a future date.");

		var entry = await _db.JournalEntries
			.Include(e => e.EntryTags).ThenInclude(et => et.Tag)
			.Include(e => e.EntryMoods).ThenInclude(em => em.Mood)
            .FirstOrDefaultAsync(e => e.EntryDate == targetDate);

        if (entry is null)
        {
            entry = new JournalEntry { EntryDate = targetDate, CreatedAt = now };
            _db.JournalEntries.Add(entry);
        }

        entry.IsDeleted = false;

        entry.Title = title?.Trim() ?? "";
		entry.Content = content ?? "";
		entry.UpdatedAt = now;
		entry.WordCount = string.IsNullOrWhiteSpace(entry.Content)
			? 0
			: entry.Content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

		// Category: find or create (supports custom categories)
		var cleanCategory = (categoryName ?? "").Trim();
		if (string.IsNullOrWhiteSpace(cleanCategory))
		{
			entry.CategoryId = null;
		}
		else
		{
			var category = await _db.Categories
				.FirstOrDefaultAsync(c => c.CategoryName == cleanCategory);

			if (category is null)
			{
				category = new Category
				{
					CategoryName = cleanCategory,
					CreatedAt = now
				};
				_db.Categories.Add(category);
			}

            // Setting the navigation is optional; FK is enough.
            entry.Category = category;
        }

		_db.EntryTags.RemoveRange(entry.EntryTags);
		entry.EntryTags.Clear();

		foreach (var tagName in tags.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			var clean = (tagName ?? "").Trim();
			if (string.IsNullOrWhiteSpace(clean)) continue;

			var tag = await _db.Tags.FirstOrDefaultAsync(t => t.TagName == clean);
			if (tag is null)
			{
				tag = new Tag { TagName = clean, CreatedAt = now, IsPrebuilt = false, UsageCount = 0 };
				_db.Tags.Add(tag);
			}

			entry.EntryTags.Add(new EntryTag
			{
				JournalEntry = entry,
				Tag = tag,
				CreatedAt = now
			});
		}

		_db.EntryMoods.RemoveRange(entry.EntryMoods);
		entry.EntryMoods.Clear();

		var moodNames = new List<(string Name, bool IsPrimary)> { (primaryMoodName, true) };
		moodNames.AddRange(secondaryMoodNames.Select(n => (n, false)));

		foreach (var (name, isPrimary) in moodNames)
		{
			var clean = (name ?? "").Trim();
			if (string.IsNullOrWhiteSpace(clean)) continue;

			var mood = await _db.Moods.FirstOrDefaultAsync(m => m.MoodName == clean);
			if (mood is null)
				throw new InvalidOperationException($"Mood '{clean}' not found in database.");

			entry.EntryMoods.Add(new EntryMood
			{
				JournalEntry = entry,
				Mood = mood,
				IsPrimary = isPrimary,
				CreatedAt = now
			});
		}
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.GetBaseException().Message; // usually contains provider message
            System.Diagnostics.Debug.WriteLine(inner);

            // Optional: show in UI/log
            throw new Exception(inner, ex);
        }

    }
}
