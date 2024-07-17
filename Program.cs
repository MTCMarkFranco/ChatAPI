using Microsoft.AspNetCore.Mvc;
using PdfProcessingApi.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// Add services to the container.

builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(x =>
{
    x.ValueLengthLimit = int.MaxValue;
    x.MultipartBodyLengthLimit = int.MaxValue; // Adjust the size as needed for large files
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// app.MapPost("/ProcessPdf", static async (HttpRequest request, PdfProcessController controller) =>
// {
   
//     var form = await request.ReadFormAsync();
//     var files = form.Files; // This is where the files are accessed

//     await controller.UploadPdfFiles(form);
    
// })
// .WithName("ProcessPdf")
// .WithFormOptions()
// .WithOpenApi();

app.Run();

