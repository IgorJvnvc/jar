package com.masan.jar;

import android.app.Application;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.media.AudioAttributes;
import android.net.Uri;
import android.os.Build;

public class MainApplication extends Application {

    private static final String DUEL_CHANNEL_ID = "duel_challenges";

    @Override
    public void onCreate() {
        super.onCreate();
        createDuelChallengeChannel();
    }

    private void createDuelChallengeChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return;
        }

        NotificationManager notificationManager = getSystemService(NotificationManager.class);
        if (notificationManager == null) {
            return;
        }

        NotificationChannel channel = new NotificationChannel(
            DUEL_CHANNEL_ID,
            "Duel Challenges",
            NotificationManager.IMPORTANCE_HIGH
        );

        channel.setDescription("High Noon duel challenge alerts");
        channel.enableVibration(true);

        Uri soundUri = Uri.parse("android.resource://" + getPackageName() + "/" + R.raw.duel_challenge);
        AudioAttributes audioAttributes = new AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_NOTIFICATION)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build();
        channel.setSound(soundUri, audioAttributes);

        notificationManager.createNotificationChannel(channel);
    }
}
