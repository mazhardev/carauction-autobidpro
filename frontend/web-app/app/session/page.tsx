import { auth } from "@/auth"
import Heading from "../components/Heading";
import AuthTest from "./AuthTest";

export default async function Session() {
    const session = await auth();

    // Never render the raw access token: this page ends up in screenshots,
    // screen shares and browser extensions.
    const {accessToken, ...safeSession} = session ?? {};
    const displayed = session
        ? {...safeSession, accessToken: accessToken ? '<redacted>' : undefined}
        : null;

    return (
        <div>
            <Heading title="Session dashboard" />
            <div className="bg-blue-200 border-2 border-blue-500">
                <h3 className="text-lg">Session data</h3>
                <pre className="whitespace-pre-wrap break-all">
                    {JSON.stringify(displayed, null, 2)}</pre>
            </div>

            <div className="mt-4">
                <AuthTest />
            </div>
        </div>
    )
}
