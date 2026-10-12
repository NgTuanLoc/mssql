using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.Lab;

// Pins the EfLab_ name guard. Needs no database for the name checks.
public class LabDatabaseTests
{
    [Theory]
    [InlineData("AdventureWorks2022")]
    [InlineData("master")]
    [InlineData("EfLab")]
    [InlineData("EfLab_")]
    [InlineData("eflab_E10")]
    [InlineData("EfLab_E10\n")]
    [InlineData("EfLab_E10]; DROP DATABASE [AdventureWorks2022")]
    public void Guard_refuses_anything_but_EfLab_names(string database)
        => Assert.Throws<InvalidOperationException>(() => LabDatabase.Guard(database));

    [Fact]
    public void Lesson_databases_are_EfLab_names()
        => Assert.Equal("EfLab_E10", LabDatabase.For("E10"));

    [Fact]
    public async Task Create_and_drop_refuse_AdventureWorks()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => LabDatabase.DropAsync(LabConnection.AdventureWorks));
        await Assert.ThrowsAsync<InvalidOperationException>(() => LabDatabase.RecreateAsync(LabConnection.AdventureWorks));
        await Assert.ThrowsAsync<InvalidOperationException>(() => LabDatabase.EnsureExistsAsync(LabConnection.AdventureWorks));
    }
}

// Pins what CodeFirstTest guarantees the code-first lessons.
public class CodeFirstSelfTests() : CodeFirstTest("LabSelfTest")
{
    public class Note
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
    }

    static void NoteModel(ModelBuilder model) => model.Entity<Note>();
    static void NotesInAnotherTable(ModelBuilder model) => model.Entity<Note>().ToTable("OtherNotes");

    [Fact]
    public async Task CreateModelAsync_creates_the_tables_and_counts_only_later_commands()
    {
        var db = await CreateModelAsync(NoteModel);

        db.Add(new Note { Text = "inside the test" });
        await db.SaveChangesAsync();

        Sql.AssertRoundTrips(1); // the INSERT; the CREATE TABLE was setup
        Assert.Equal(1, (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM [Note]")).Single());
    }

    [Fact]
    public void Each_configuration_method_gets_its_own_model()
    {
        var first = NewContext(NoteModel);
        var second = NewContext(NotesInAnotherTable);

        Assert.Equal("Note", first.Model.FindEntityType(typeof(Note))!.GetTableName());
        Assert.Equal("OtherNotes", second.Model.FindEntityType(typeof(Note))!.GetTableName());
    }

    [Fact]
    public void A_lambda_that_captures_a_local_is_refused()
    {
        var table = "X";

        Assert.Throws<InvalidOperationException>(() => NewContext(m => m.Entity<Note>().ToTable(table)).Model);
    }

    [Fact]
    public void Capture_free_lambdas_and_lambdas_capturing_this_are_accepted()
    {
        Assert.Equal("Note", NewContext(m => m.Entity<Note>()).Model.FindEntityType(typeof(Note))!.GetTableName());
        Assert.Equal("Note", NewContext(m => m.Entity<Note>().ToTable(NoteTable)).Model.FindEntityType(typeof(Note))!.GetTableName());
    }

    string NoteTable => "Note";

    [Fact]
    public async Task Commits_are_refused_here_too()
    {
        var db = await CreateModelAsync(NoteModel);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Database.CommitTransactionAsync());
    }

    [Fact]
    public async Task Tables_and_rows_are_gone_after_the_test()
    {
        var lab = new ScratchLab();
        await lab.InitializeAsync();
        try
        {
            await lab.CreateAndFill();
        }
        finally
        {
            await lab.DisposeAsync();
        }

        await using var connection = await LabConnection.OpenAsync(Database);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables", connection);
        Assert.Equal(0, await command.ExecuteScalarAsync());
    }

    sealed class ScratchLab() : CodeFirstTest("LabSelfTest")
    {
        public async Task CreateAndFill()
        {
            var db = await CreateModelAsync(NoteModel);
            db.Add(new Note { Text = "never committed" });
            await db.SaveChangesAsync();
        }
    }
}
