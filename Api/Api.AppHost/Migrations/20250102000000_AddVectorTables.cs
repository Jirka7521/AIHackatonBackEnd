using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.AppHost.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create source_files table
            migrationBuilder.CreateTable(
                name: "source_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FilePath = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_files", x => x.Id);
                });

            // Create vectors table
            migrationBuilder.CreateTable(
                name: "vectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceFileId = table.Column<int>(type: "integer", nullable: false),
                    VectorData = table.Column<float[]>(type: "vector(1536)", nullable: false),
                    Snippet = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vectors_source_files_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "source_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create index for foreign key
            migrationBuilder.CreateIndex(
                name: "IX_vectors_SourceFileId",
                table: "vectors",
                column: "SourceFileId");

            // Enable vector extension if not exists
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            // Create cosine similarity function
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION public.cosine_similarity(vec vector, arr real[])
                RETURNS double precision AS $$
                DECLARE
                    dot double precision;
                    norm_vec double precision;
                    norm_arr double precision;
                BEGIN
                    SELECT SUM(val1 * val2) INTO dot
                    FROM (
                        SELECT unnest(string_to_array(trim(both '[]' from vec::text), ','))::double precision AS val1,
                               unnest(arr)::double precision AS val2
                    ) s;
                    
                    SELECT sqrt(SUM(val1 * val1)) INTO norm_vec
                    FROM (
                        SELECT unnest(string_to_array(trim(both '[]' from vec::text), ','))::double precision AS val1
                    ) s;
                    
                    SELECT sqrt(SUM(val2 * val2)) INTO norm_arr
                    FROM (
                        SELECT unnest(arr)::double precision AS val2
                    ) s;
                    
                    IF norm_vec = 0 OR norm_arr = 0 THEN
                         RETURN 0;
                    END IF;
                    
                    RETURN dot / (norm_vec * norm_arr);
                END;
                $$ LANGUAGE plpgsql IMMUTABLE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop cosine similarity function
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.cosine_similarity(vector, real[]);");

            // Drop tables
            migrationBuilder.DropTable(
                name: "vectors");

            migrationBuilder.DropTable(
                name: "source_files");
        }
    }
} 