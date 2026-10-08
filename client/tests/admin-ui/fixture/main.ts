import { mount } from 'svelte';
import './styles.css';
import Fixture from './Fixture.svelte';
mount(Fixture, { target: document.getElementById('app')! });
