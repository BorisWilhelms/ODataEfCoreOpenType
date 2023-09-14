using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.ModelBuilder;
using Microsoft.OData.UriParser;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<PersonDbContext>(b =>
    b.UseSqlite("Data Source=data.db;"));

builder.Services
    .AddControllers()
    .AddOData(o =>
    {
        o.EnableQueryFeatures(int.MaxValue);
        o.AddRouteComponents("odata", new ModelBuilder().GetEdmModel());
        o.RouteOptions.EnableDollarCountRouting = false;
        o.RouteOptions.EnableKeyAsSegment = false;
    });

var app = builder.Build();

app.MapControllers();

app.Run();

public class ModelBuilder : ODataConventionModelBuilder
{
    public ModelBuilder()
    {
        this.EntitySet<Person>("Person");
        this.EntityType<Person>().Ignore(p => p.CustomAttributes);
    }
}

public class PersonController : ODataController
{
    private readonly PersonDbContext _dbContext;

    public PersonController(PersonDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [EnableQuery]
    public ActionResult<IQueryable<Person>> Get(ODataQueryOptions<Person> options)
    {
        var query = _dbContext.Persons.AsQueryable();
        
        // Check if a property is select, that does not belong to the entity and assume
        // it is a custom attribute
        var items = options.SelectExpand?.SelectExpandClause?.SelectedItems.OfType<PathSelectItem>();
        var attributeSelected = items?.SelectMany(i => i.SelectedPath).OfType<DynamicPathSegment>().Any();
        if (attributeSelected.HasValue && attributeSelected.Value)
        {
            query = query.Include(e => e.CustomAttributes);
        }

        return Ok(query);
    }
}


public class Person
{
    public int Id { get; set; }

    public string Name { get; set; }

    public ICollection<PersonCustomAttribute> CustomAttributes { get; set; }

    public Dictionary<string, object> Properties => CustomAttributes?.ToDictionary(a => a.Key, a => (object)a.Value);
}

public class PersonCustomAttribute
{
    public int Id { get; set; }

    public int PersonId { get; set; }

    public string Key { get; set; }

    public string Value { get; set; }
}

public class PersonDbContext : DbContext
{
    public PersonDbContext()
    {
    }

    public PersonDbContext(DbContextOptions<PersonDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>().Ignore(p => p.Properties);
    }

    public DbSet<Person> Persons => Set<Person>();
}