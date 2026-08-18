import EmptyFilter from "@/app/components/EmptyFilter";

// Next.js 15 hands `searchParams` to pages as a promise.
export default async function Page({searchParams}: {searchParams: Promise<{callbackUrl?: string}>}) {
  const {callbackUrl} = await searchParams;

  return (
    <EmptyFilter
        title='You need to be logged in to do that'
        subtitle="Please click below to login"
        showLogin
        callbackUrl={callbackUrl}
    />
  )
}
