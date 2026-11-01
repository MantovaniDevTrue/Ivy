package dev.mantovani.ivy.panel;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.net.Uri;
import android.util.Log;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;

/** Installs before MonoRuntimeProvider, including failures before the C# service exists. */
public final class StartupDiagnostics extends ContentProvider {
    private static final Object lock = new Object();
    private static File output;

    private static void append(String text) {
        synchronized (lock) {
            try {
                if (output.length() > 131072) {
                    // Keep one prior process attempt without accumulating reports indefinitely.
                    File previous = new File(output.getPath() + ".previous");
                    if (previous.exists()) previous.delete();
                    output.renameTo(previous);
                }
                try (FileOutputStream stream = new FileOutputStream(output, true)) {
                    stream.write(text.getBytes(StandardCharsets.UTF_8));
                    stream.flush();
                }
            } catch (Throwable ignored) {
                // Diagnostics must never prevent the original Android crash handler from running.
            }
        }
    }

    @Override public boolean onCreate() {
        output = new File(getContext().getFilesDir(), "ivy-android-startup.txt");
        append("\n--- Android bootstrap Ivy 0.8.0, PID=" + android.os.Process.myPid()
                + ", timestamp=" + System.currentTimeMillis() + " ---\nBefore MonoRuntimeProvider\n");
        final Thread.UncaughtExceptionHandler original = Thread.getDefaultUncaughtExceptionHandler();
        Thread.setDefaultUncaughtExceptionHandler((thread, error) -> {
            try {
                append("UNCAUGHT thread=" + thread.getName() + "\n" + Log.getStackTraceString(error) + "\n");
            } finally {
                if (original != null) original.uncaughtException(thread, error);
                else {
                    android.os.Process.killProcess(android.os.Process.myPid());
                    System.exit(10);
                }
            }
        });
        return true;
    }

    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] args, String sort) { return null; }
    @Override public String getType(Uri uri) { return null; }
    @Override public Uri insert(Uri uri, ContentValues values) { return null; }
    @Override public int delete(Uri uri, String selection, String[] args) { return 0; }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] args) { return 0; }
}
