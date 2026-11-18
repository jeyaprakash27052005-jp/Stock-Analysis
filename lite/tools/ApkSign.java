import com.android.apksig.ApkSigner;
import com.android.apksig.ApkVerifier;
import java.io.File;
import java.io.FileInputStream;
import java.security.KeyStore;
import java.security.PrivateKey;
import java.security.cert.X509Certificate;
import java.util.Collections;

/** Signs an APK with APK Signature Scheme v2 (apksig) and verifies the result.
 *  Usage: java ApkSign <in.apk> <out.apk> <keystore.p12> <storepass> <alias> */
public class ApkSign {
    public static void main(String[] a) throws Exception {
        KeyStore ks = KeyStore.getInstance("PKCS12");
        try (FileInputStream in = new FileInputStream(a[2])) { ks.load(in, a[3].toCharArray()); }
        PrivateKey key = (PrivateKey) ks.getKey(a[4], a[3].toCharArray());
        X509Certificate cert = (X509Certificate) ks.getCertificate(a[4]);
        ApkSigner.SignerConfig cfg = new ApkSigner.SignerConfig.Builder("CERT", key, Collections.singletonList(cert)).build();
        new ApkSigner.Builder(Collections.singletonList(cfg))
            .setInputApk(new File(a[0])).setOutputApk(new File(a[1]))
            .setMinSdkVersion(24).setV1SigningEnabled(false).setV2SigningEnabled(true)   // v2 covers Android 7.0+ (minSdk 24)
            .setCreatedBy("Stormdrop build").build().sign();
        ApkVerifier.Result r = new ApkVerifier.Builder(new File(a[1])).build().verify();
        System.out.println("verified=" + r.isVerified() + " v2=" + r.isVerifiedUsingV2Scheme());
        if (!r.isVerified()) { System.out.println(r.getErrors()); System.exit(1); }
    }
}
