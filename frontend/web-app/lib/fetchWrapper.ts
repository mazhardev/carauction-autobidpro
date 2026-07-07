import { auth } from "@/auth";

const baseUrl = 'http://localhost:6001/';

async function get(url: string) {
    const requestOptions = {
        method: 'GET',
        headers: await getHeaders()
    };
    const response = await fetch(baseUrl + url, requestOptions);
    return handleResponse(response);
}

async function put(url: string, body: unknown) {
    const requestOptions = {
        method: 'PUT',
        headers: await getHeaders(),
        body: JSON.stringify(body)
    };
    const response = await fetch(baseUrl + url, requestOptions);
    return handleResponse(response);
}

async function post(url: string, body: unknown) {
    const requestOptions = {
        method: 'POST',
        headers: await getHeaders(),
        body: JSON.stringify(body)
    };
    const response = await fetch(baseUrl + url, requestOptions);
    return handleResponse(response);
}

async function del(url: string) {
    const requestOptions = {
        method: 'DELETE',
        headers: await getHeaders()
    };
    const response = await fetch(baseUrl + url, requestOptions);
    return handleResponse(response);
}

async function handleResponse(response: Response) {
    const text = await response.text();
    let data;

    try {
        data = text ? JSON.parse(text) : null;
    } catch {
        data = text;
    }

    if (response.ok) {
        return data || response.statusText;
    } else {
        const error = {
            status: response.status,
            message: getErrorMessage(data, response.statusText)
        }
        return {error}
    }
}

function getErrorMessage(data: unknown, fallback: string) {
    if (typeof data === 'string') return data;

    if (data && typeof data === 'object') {
        if ('errors' in data && data.errors && typeof data.errors === 'object') {
            const messages = Object.values(data.errors)
                .flat()
                .filter((message): message is string => typeof message === 'string');

            if (messages.length > 0) return messages.join('\n');
        }

        if ('title' in data && typeof data.title === 'string') {
            return data.title;
        }
    }

    return fallback;
}

async function getHeaders(): Promise<Headers> {
    const session = await auth();
    const headers = new Headers();
    headers.set('Content-type', 'application/json');
    if (session) {
        headers.set('Authorization', 'Bearer ' + session.accessToken)
    }
    return headers;
}

export const fetchWrapper = {
    get,
    post,
    put,
    del
}
