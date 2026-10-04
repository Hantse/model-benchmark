import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import App from './App';
import { makeStore } from './store';
createRoot(document.getElementById('root')!).render(<Provider store={makeStore()}><App /></Provider>);
