using HeavenTool.Forms.BCSV.Controls.Entries;
using HeavenTool.IO;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Forms.BCSV.Controls;

public partial class HashEntry : BCSVEntry
{
    public sealed record HashComboBoxEntry(string Name, uint Value);

    private readonly uint fieldHash;
    private readonly bool isMurmur;
    private readonly uint initialValue;
    private readonly CancellationTokenSource suggestionCancellation = new();
    private bool suggestionsLoading;
    private bool suggestionsLoaded;
    private bool suggestionCancellationDisposed;

    public Action<object>? Callback;

    public HashEntry(object defaultValue, uint columnHash, bool isMurmurHash = false)
    {
        InitializeComponent();

        if (defaultValue is not uint value)
            throw new ArgumentException("Default value for a hash field must be a uint", nameof(defaultValue));

        fieldHash = columnHash;
        isMurmur = isMurmurHash;
        initialValue = value;

        crcComboBox.MaxDropDownItems = 10;
        crcComboBox.DisplayMember = "Name";
        crcComboBox.ValueMember = "Value";

        var name = isMurmur ? value.GetMurmurHashTranslationOrNull() : value.GetHashTranslationOrNull();
        crcComboBox.Items.Add(new HashComboBoxEntry(name ?? $"0x{value:X8}", value));
        crcComboBox.SelectedIndex = 0;
        crcComboBox.DropDown += LoadSuggestions;
        crcComboBox.TextChanged += (_, _) => Callback?.Invoke(GetValue());
        Disposed += (_, _) =>
        {
            if (suggestionCancellationDisposed)
                return;
            suggestionCancellationDisposed = true;
            suggestionCancellation.Cancel();
            suggestionCancellation.Dispose();
        };
    }

    private async void LoadSuggestions(object? sender, EventArgs e)
    {
        if (suggestionsLoading || suggestionsLoaded || IsDisposed || Disposing)
            return;

        suggestionsLoading = true;
        var cancellation = suggestionCancellation.Token;
        try
        {
            // Yield before preparing suggestions so opening the dropdown can paint.
            await Task.Delay(1, cancellation);
            var items = await HashEntrySuggestions.GetAsync(fieldHash, isMurmur).WaitAsync(cancellation);
            const int batchSize = 64;
            for (int start = 0; start < items.Length; start += batchSize)
            {
                cancellation.ThrowIfCancellationRequested();
                crcComboBox.BeginUpdate();
                try
                {
                    int end = Math.Min(start + batchSize, items.Length);
                    for (int i = start; i < end; i++)
                        if (items[i].Value != initialValue)
                            crcComboBox.Items.Add(items[i]);
                }
                finally
                {
                    if (!crcComboBox.IsDisposed)
                        crcComboBox.EndUpdate();
                }
                if (start + batchSize < items.Length)
                    await Task.Delay(1, cancellation);
            }
            suggestionsLoaded = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The entry was disposed while its suggestions were loading.
        }
        finally
        {
            suggestionsLoading = false;
        }
    }

    public override void SetCallback(Action<object> newValueCallback)
    {
        Callback = newValueCallback;
    }

    public override void SetPropertyName(string name)
    {
        propertyNameLabel.Text = name;
    }

    public override void SetUniqueIdentifier()
    {
        propertyNameLabel.Font = new System.Drawing.Font(DefaultFont, System.Drawing.FontStyle.Bold);
    }

    public override object GetValue()
    {
        if (crcComboBox.SelectedItem is HashComboBoxEntry entry && crcComboBox.Text == entry.Name)
            return entry.Value;

        string text = crcComboBox.Text;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && uint.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw))
            return raw;

        return isMurmur ? text.ToMurmur() : text.ToCRC32();
    }
}