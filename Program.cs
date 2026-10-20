namespace PixelVault;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        try { Api.Iniciar(); }   // cria o banco SQLite local na primeira execução
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível abrir o banco de dados:\n\n" + ex.Message, "PixelVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Application.Run(new MainForm());
    }
}
