package dev.mantovani.ivy.panel;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.*;
import java.util.ArrayList;
import java.util.Locale;
import org.json.*;

/** Native presentation only. All loader operations are delegated to the existing C# controller. */
public final class IvyPanelView extends FrameLayout {
  private final Activity activity;
  private final View.OnClickListener actions;
  private final boolean hasExample;
  private final SharedPreferences preferences;
  private final FrameLayout content;
  private final TextView status;
  private final Button[] tabs = new Button[4];
  private final ArrayList<JSONObject> installed = new ArrayList<>();
  private final ArrayList<JSONObject> filtered = new ArrayList<>();
  private TextView summary, session, environment, reportView, modsSummary;
  private Button play;
  private EditText search;
  private ModAdapter adapter;
  private String report = "", sessionText = "Nenhuma sessão confirmada", filter = "";
  private int page = 0;
  private boolean busy = false, libraryReady = false;

  public IvyPanelView(
      Activity activity, View.OnClickListener actions, String version, boolean hasExample) {
    super(activity);
    this.activity = activity;
    this.actions = actions;
    this.hasExample = hasExample;
    preferences = activity.getSharedPreferences("ivy.panel", Context.MODE_PRIVATE);
    LayoutInflater.from(activity).inflate(id("ivy_panel", "layout"), this, true);
    ((TextView) findViewById(id("ivy_version", "id"))).setText(version);
    content = findViewById(id("ivy_content", "id"));
    status = findViewById(id("ivy_status", "id"));
    LinearLayout nav = findViewById(id("ivy_navigation", "id"));
    String[] labels = {s("ivy_home"), s("ivy_mods"), s("ivy_settings"), s("ivy_reports")};
    for (int i = 0; i < 4; i++) {
      final int index = i;
      Button b = button(labels[i], null, false);
      b.setTextSize(11);
      b.setPadding(0, 0, 0, 0);
      b.setOnClickListener(v -> show(index));
      tabs[i] = b;
      LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(0, dp(48), 1);
      p.setMargins(dp(2), 0, dp(2), 0);
      nav.addView(b, p);
    }
    show(0);
  }

  private int id(String name, String type) {
    return activity.getResources().getIdentifier(name, type, activity.getPackageName());
  }

  private String s(String key) {
    return activity.getString(id(key, "string"));
  }

  private int dp(int n) {
    return Math.round(n * getResources().getDisplayMetrics().density);
  }

  private TextView text(String value, int size, boolean bold, boolean muted) {
    TextView t = new TextView(activity);
    t.setText(value);
    t.setTextSize(size * (preferences.getBoolean("large_text", false) ? 1.1f : 1f));
    t.setTextColor(Color.parseColor(muted ? "#A8BCAE" : "#EDF5EF"));
    if (bold) t.setTypeface(null, Typeface.BOLD);
    t.setLineSpacing(dp(3), 1);
    return t;
  }

  private LinearLayout column() {
    LinearLayout p = new LinearLayout(activity);
    p.setOrientation(LinearLayout.VERTICAL);
    return p;
  }

  private void add(LinearLayout p, View v, int margin) {
    LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(-1, -2);
    lp.setMargins(0, 0, 0, dp(margin));
    p.addView(v, lp);
  }

  private Button button(String label, String action, boolean primary) {
    Button b = new Button(activity);
    b.setText(label);
    b.setAllCaps(false);
    b.setTextSize(15);
    b.setMinHeight(dp(52));
    b.setGravity(Gravity.CENTER);
    b.setTextColor(Color.parseColor(primary ? "#173B25" : "#EDF5EF"));
    b.setBackgroundResource(id(primary ? "ivy_primary" : "ivy_button", "drawable"));
    if (action != null) {
      b.setTag(action);
      b.setOnClickListener(actions);
    }
    return b;
  }

  private LinearLayout card() {
    LinearLayout c = column();
    c.setBackgroundResource(id("ivy_card", "drawable"));
    return c;
  }

  private ScrollView scroll(LinearLayout body) {
    ScrollView scroll = new ScrollView(activity);
    scroll.setFillViewport(false);
    scroll.setClipToPadding(false);
    scroll.setPadding(dp(4), dp(6), dp(4), dp(12));
    scroll.addView(body);
    return scroll;
  }

  public void setStatus(String value) {
    status.setText(value);
  }

  public void setSession(String value) {
    sessionText = value;
    if (session != null) session.setText(value);
  }

  public void setBusy(boolean value) {
    busy = value;
    if (play != null) {
      play.setEnabled(!value && libraryReady);
      play.setAlpha(value ? .6f : 1);
      play.setText(value ? "Preparando mods…" : s("ivy_play"));
    }
  }

  public void setReport(String value) {
    report = value == null ? "" : value;
    if (reportView != null)
      reportView.setText(
          report.length() > 16000
              ? "… últimas mensagens …\n" + report.substring(report.length() - 16000)
              : report);
  }

  public void setLibrary(String json) {
    try {
      JSONArray list = new JSONArray(json);
      installed.clear();
      for (int i = 0; i < list.length(); i++) installed.add(list.getJSONObject(i));
      libraryReady = true;
      updateSummary();
      applyFilter();
      setBusy(busy);
    } catch (JSONException ex) {
      setStatus("Não foi possível exibir a biblioteca: " + ex.getMessage());
    }
  }

  private int enabledCount() {
    int n = 0;
    for (JSONObject m : installed) if (m.optBoolean("enabled")) n++;
    return n;
  }

  private void updateSummary() {
    if (summary != null)
      summary.setText(
          libraryReady ? enabledCount() + " mod(s) selecionado(s)" : "Carregando biblioteca…");
    if (modsSummary != null)
      modsSummary.setText(
          installed.isEmpty()
              ? "Sua biblioteca está vazia. Você já pode jogar sem mods."
              : installed.size() + " instalado(s) · " + enabledCount() + " selecionado(s)");
  }

  private void applyFilter() {
    filtered.clear();
    for (JSONObject m : installed)
      if (m.optString("name").toLowerCase(Locale.ROOT).contains(filter.toLowerCase(Locale.ROOT)))
        filtered.add(m);
    if (adapter != null) adapter.notifyDataSetChanged();
  }

  private void show(int index) {
    page = index;
    content.removeAllViews();
    summary = session = environment = reportView = modsSummary = null;
    play = null;
    search = null;
    adapter = null;
    for (int i = 0; i < 4; i++) {
      tabs[i].setAlpha(i == page ? 1 : .58f);
      tabs[i].setTypeface(null, i == page ? Typeface.BOLD : Typeface.NORMAL);
    }
    if (page == 0) home();
    else if (page == 1) mods();
    else if (page == 2) settings();
    else reports();
  }

  public String saveState() {
    try {
      return new JSONObject().put("page", page).put("filter", filter).toString();
    } catch (JSONException ex) {
      return "{}";
    }
  }

  public void restoreState(String json) {
    try {
      JSONObject state = new JSONObject(json);
      filter = state.optString("filter", "");
      show(Math.max(0, Math.min(3, state.optInt("page", 0))));
    } catch (JSONException ex) {
      show(0);
    }
  }

  public boolean goBack() {
    if (page == 0) return false;
    show(0);
    return true;
  }

  private void home() {
    LinearLayout body = column();
    add(body, text(s("ivy_ui_6b8ab97061"), 34, true, false), 12);
    add(body, text(s("ivy_ui_60ea35ba52"), 15, false, true), 22);
    LinearLayout hero = card();
    add(hero, text(s("ivy_ui_2f7011b4db"), 11, true, true), 10);
    summary = text("", 21, true, false);
    add(hero, summary, 8);
    add(hero, text(s("ivy_ui_ea2183c589"), 14, false, true), 18);
    play = button(s("ivy_play"), "play", true);
    add(hero, play, 0);
    add(body, hero, 14);
    LinearLayout state = card();
    add(state, text(s("ivy_ui_0d2b7ca656"), 17, true, false), 8);
    environment = text(s("ivy_ui_78a4906fef"), 14, false, true);
    add(state, environment, 6);
    session = text(sessionText, 14, false, true);
    add(state, session, 0);
    add(body, state, 14);
    Button mods = button(s("ivy_ui_e4e5ff38ad"), null, false);
    mods.setOnClickListener(v -> show(1));
    add(body, mods, 10);
    add(body, button(s("ivy_ui_6477f0b83f"), "prepare", false), 18);
    add(body, text(s("ivy_ui_a36cb05e27"), 10, true, true), 12);
    content.addView(scroll(body));
    updateSummary();
    setBusy(busy);
  }

  private void mods() {
    LinearLayout body = column();
    body.setPadding(dp(4), dp(6), dp(4), 0);
    add(body, text(s("ivy_ui_f8fbef3fb3"), 28, true, false), 14);
    search = new EditText(activity);
    search.setSingleLine(true);
    search.setTextColor(Color.WHITE);
    search.setHintTextColor(Color.parseColor("#A8BCAE"));
    search.setHint(s("ivy_search"));
    search.setText(filter);
    search.setMinHeight(dp(52));
    LinearLayout toolbar = new LinearLayout(activity);
    toolbar.setGravity(Gravity.CENTER_VERTICAL);
    LinearLayout.LayoutParams searchParams = new LinearLayout.LayoutParams(0, dp(52), 1);
    searchParams.setMargins(0, 0, dp(8), 0);
    toolbar.addView(search, searchParams);
    toolbar.addView(
        button(s("ivy_import"), "import", true), new LinearLayout.LayoutParams(dp(110), dp(52)));
    add(body, toolbar, 12);
    search.addTextChangedListener(
        new TextWatcher() {
          public void beforeTextChanged(CharSequence s, int start, int count, int after) {}

          public void onTextChanged(CharSequence s, int start, int before, int count) {
            filter = s.toString();
            applyFilter();
          }

          public void afterTextChanged(Editable e) {}
        });
    modsSummary = text("", 13, false, true);
    add(body, modsSummary, 8);
    add(body, text(s("ivy_ui_9f73687b9f"), 12, false, true), 10);
    ListView list = new ListView(activity);
    list.setDivider(null);
    list.setCacheColorHint(Color.TRANSPARENT);
    adapter = new ModAdapter();
    list.setAdapter(adapter);
    TextView empty = text(s("ivy_ui_443c9a13dd"), 15, false, true);
    body.addView(empty);
    list.setEmptyView(empty);
    body.addView(list, new LinearLayout.LayoutParams(-1, 0, 1));
    list.setOnItemClickListener((parent, view, position, rowId) -> details(filtered.get(position)));
    content.addView(body, new FrameLayout.LayoutParams(-1, -1));
    updateSummary();
    applyFilter();
  }

  private void details(JSONObject m) {
    String message =
        "Versão: "
            + m.optString("version")
            + "\n"
            + (m.isNull("problem") ? "Metadados aceitos pelo SDK" : m.optString("problem"))
            + "\n\nSHA-256\n"
            + m.optString("hash");
    new AlertDialog.Builder(activity)
        .setTitle(m.optString("name"))
        .setMessage(message)
        .setPositiveButton("Fechar", null)
        .show();
  }

  private void settings() {
    LinearLayout body = column();
    add(body, text(s("ivy_ui_4f2debbbf4"), 28, true, false), 16);
    LinearLayout appearance = card();
    add(appearance, text(s("ivy_ui_04f9f0a78d"), 18, true, false), 10);
    add(appearance, text(s("ivy_ui_f722ef868e"), 13, false, true), 10);
    Switch large = new Switch(activity);
    large.setText("Texto maior");
    large.setTextColor(Color.WHITE);
    large.setMinHeight(dp(52));
    large.setChecked(preferences.getBoolean("large_text", false));
    large.setOnCheckedChangeListener(
        (view, value) -> {
          preferences.edit().putBoolean("large_text", value).apply();
          show(2);
        });
    add(appearance, large, 0);
    add(body, appearance, 14);
    LinearLayout sessionCard = card();
    add(sessionCard, text(s("ivy_ui_e6f5e06079"), 18, true, false), 12);
    add(sessionCard, button(s("ivy_ui_9d8a5c401a"), "unload", false), 10);
    add(sessionCard, button(s("ivy_ui_578cdfc9cb"), "disable_all", false), 0);
    add(body, sessionCard, 14);
    if (hasExample) {
      LinearLayout dev = card();
      add(dev, text(s("ivy_ui_c36c2f69fb"), 17, true, false), 10);
      add(dev, button(s("ivy_ui_619f71a59b"), "example", false), 10);
      add(dev, button(s("ivy_ui_b7a8aeb5dd"), "stop_bridge", false), 0);
      add(body, dev, 14);
    }
    add(body, text(s("ivy_ui_5e96184669"), 13, false, true), 12);
    content.addView(scroll(body));
  }

  private void reports() {
    LinearLayout body = column();
    add(body, text(s("ivy_ui_dc4bb10b6d"), 28, true, false), 10);
    add(body, text(s("ivy_ui_08e45b1adb"), 13, false, true), 16);
    add(body, button(s("ivy_ui_6b782b6426"), "refresh_reports", false), 10);
    add(body, button(s("ivy_export"), "export", true), 10);
    Button copy = button(s("ivy_ui_9bb126076d"), null, false);
    copy.setOnClickListener(
        v -> {
          ((ClipboardManager) activity.getSystemService(Context.CLIPBOARD_SERVICE))
              .setPrimaryClip(ClipData.newPlainText("Ivy", report));
          setStatus("Relatório copiado.");
        });
    add(body, copy, 16);
    reportView = text("", 12, false, true);
    reportView.setTextIsSelectable(true);
    add(body, reportView, 0);
    content.addView(scroll(body));
    setReport(report);
  }

  private final class ModAdapter extends BaseAdapter {
    public int getCount() {
      return filtered.size();
    }

    public JSONObject getItem(int p) {
      return filtered.get(p);
    }

    public long getItemId(int p) {
      return p;
    }

    public View getView(int position, View recycled, ViewGroup parent) {
      Row row;
      if (recycled == null) {
        LinearLayout container = column();
        container.setPadding(dp(8), dp(14), dp(8), dp(14));
        row = new Row();
        row.name = text("", 17, true, false);
        row.detail = text("", 12, false, true);
        row.toggle = new Switch(activity);
        row.toggle.setTextColor(Color.WHITE);
        row.toggle.setText("Ativar na próxima sessão");
        row.toggle.setMinHeight(dp(48));
        container.addView(row.name);
        container.addView(row.detail);
        container.addView(row.toggle);
        container.setTag(row);
        recycled = container;
        final Row holder = row;
        container.setOnClickListener(v -> details(holder.mod));
        row.toggle.setFocusable(false);
        row.toggle.setOnCheckedChangeListener(
            (view, value) -> {
              if (holder.binding) return;
              view.setEnabled(false);
              view.setTag("toggle|" + holder.mod.optString("id") + "|" + (value ? "1" : "0"));
              actions.onClick(view);
            });
      } else row = (Row) recycled.getTag();
      JSONObject m = getItem(position);
      row.binding = true;
      row.mod = m;
      row.name.setText(m.optString("name"));
      row.detail.setText(
          "v"
              + m.optString("version")
              + " · "
              + (m.isNull("problem") ? "Metadados compatíveis" : m.optString("problem")));
      row.toggle.setChecked(m.optBoolean("enabled"));
      row.toggle.setEnabled(m.optBoolean("enabled") || m.isNull("problem"));
      row.binding = false;
      return recycled;
    }
  }

  private static final class Row {
    TextView name, detail;
    Switch toggle;
    JSONObject mod;
    boolean binding;
  }
}
